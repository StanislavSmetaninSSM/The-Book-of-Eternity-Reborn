namespace BookOfEternityClient.CommandProtocol;

public static class ExplorerCommandMigrationRegistry
{
    public static IReadOnlyList<ExplorerCommandMigrationEntry> Entries { get; } =
        ExplorerCommandCatalog.Descriptors
            .SelectMany(static descriptor => descriptor.Aliases.Select(alias => new ExplorerCommandMigrationEntry(
                alias,
                descriptor.Group,
                descriptor.BrowserStatus,
                descriptor.FollowUpIssue,
                descriptor.Reason)))
            .ToArray();

    public static bool IsBrowserExecutable(ExplorerCommandMigrationStatus status) =>
        status is ExplorerCommandMigrationStatus.ReadOnlyParity
            or ExplorerCommandMigrationStatus.InteractiveFormPending
            or ExplorerCommandMigrationStatus.MutatingParity
            or ExplorerCommandMigrationStatus.StatusOnly;
}

public sealed record ExplorerCommandMigrationEntry(
    string Command,
    ExplorerCommandGroup Group,
    ExplorerCommandMigrationStatus Status,
    string FollowUpIssue,
    string Reason = "");

public enum ExplorerCommandMigrationStatus
{
    ReadOnlyParity,
    InteractiveFormPending,
    MutatingParity,
    StatusOnly,
    Planned,
    Blocked,
    ConsoleOnlyTemporarily
}

public enum ExplorerCommandGroup
{
    UniversalMeta,
    MortalWorld,
    ChaosSea,
    ShiningAbode,
    AfterlifeCombatAndEntities,
    SarefStory,
    Lifecycle
}
