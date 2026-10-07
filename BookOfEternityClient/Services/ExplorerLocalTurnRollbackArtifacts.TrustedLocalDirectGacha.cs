namespace BookOfEternityClient.Services;

public static partial class ExplorerLocalTurnRollbackArtifacts
{
    private const string DirectGachaRoot = Root + "/browser_direct_gacha";
    private const string DirectGachaSoulBackupPrefix = "game_state_meta_soul_state.json.rollback.";

    // Structural storage recognition only. This neither authorizes a new turn
    // to adopt a before-image nor grants a main/worker pin from an artifact name.
    internal static bool IsLocalDirectGachaPath(string path)
    {
        if (path == DirectGachaRoot) return true;
        if (!path.StartsWith(DirectGachaRoot + "/", StringComparison.Ordinal)) return false;
        var parts = path[(DirectGachaRoot.Length + 1)..].Split('/');
        if (parts.Length is < 1 or > 2 || !IsValidTransactionDirectoryName(parts[0])) return false;
        if (parts.Length == 1) return true;
        return parts[1].StartsWith(DirectGachaSoulBackupPrefix, StringComparison.Ordinal) &&
            Guid.TryParseExact(parts[1][DirectGachaSoulBackupPrefix.Length..], "N", out var id) &&
            parts[1][DirectGachaSoulBackupPrefix.Length..] == id.ToString("N");
    }

    internal static bool IsLocalDirectGachaBackup(string path) =>
        IsLocalDirectGachaPath(path) && path.Count(character => character == '/') == DirectGachaRoot.Count(character => character == '/') + 2;
}
