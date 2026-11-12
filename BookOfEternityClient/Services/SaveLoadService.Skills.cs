using System.IO.Compression;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class SaveLoadService
{
    private async Task ValidateCanonicalSkillMasteryForSaveAsync(FileSystemManager.CanonicalWriteLease lease) =>
        RequireCanonicalSkillMasteryClosure(
            await ReadCanonicalStateForSaveAsync(lease, ValidationService.CanonicalActiveSkillsPath),
            await ReadCanonicalStateForSaveAsync(lease, ValidationService.CanonicalSkillMasteryPath));

    private static async Task ValidateArchivedSkillMasteryAsync(IReadOnlyDictionary<string, ZipArchiveEntry> entries) =>
        RequireCanonicalSkillMasteryClosure(
            await ReadOptionalArchiveEntryAsync(entries, ValidationService.CanonicalActiveSkillsPath),
            await ReadOptionalArchiveEntryAsync(entries, ValidationService.CanonicalSkillMasteryPath));

    private static void RequireCanonicalSkillMasteryClosure(string? activeJson, string? masteryJson)
    {
        var issues = ValidationService.ValidateCanonicalPlayerSkillMastery(activeJson, masteryJson);
        if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
            throw new InvalidDataException("Canonical skill mastery admission failed: " +
                string.Join("; ", issues.Select(issue => $"{issue.Code}: {issue.FilePath}")));
    }
}
