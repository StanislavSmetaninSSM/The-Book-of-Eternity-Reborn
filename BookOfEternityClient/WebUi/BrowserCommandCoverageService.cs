using BookOfEternityClient.CommandProtocol;

namespace BookOfEternityClient.WebUi;

public static class BrowserCommandCoverageService
{
    private const int SchemaVersion = 1;

    public static BrowserCommandCoverageDto Build()
    {
        var commands = ExplorerCommandCatalog.Descriptors
            .Select(BuildEntry)
            .ToArray();

        var summary = new BrowserCommandCoverageSummaryDto(
            DescriptorCount: commands.Length,
            AliasCount: commands.Sum(static command => command.Aliases.Count),
            SubcommandCount: commands.Sum(static command => command.Subcommands.Count),
            BrowserExecutableCount: commands.Count(static command => IsBrowserExecutable(command.BrowserStatus)),
            PlayerDefaultActionCount: commands.Count(static command => command.Surface == "player-default" && IsBrowserExecutable(command.BrowserStatus)),
            AdvancedOnlyActionCount: commands.Count(static command => command.Surface == "advanced-only"),
            MutatingCommandCount: commands.Count(static command => command.MutationMode == nameof(ExplorerCommandMutationMode.LocalTurn)),
            CommandsNeedingFollowUpCount: commands.Count(static command => RequiresFollowUp(command.BrowserStatus)));

        return new BrowserCommandCoverageDto(SchemaVersion, summary, commands);
    }

    private static BrowserCommandCoverageEntryDto BuildEntry(ExplorerCommandDescriptor descriptor)
    {
        var metadata = BrowserPlayerCommandMenuBuilder.GetCoverageMetadata(descriptor);
        var subcommands = descriptor.SubcommandDescriptors
            .Select(subcommand => BuildSubcommand(descriptor, subcommand, metadata))
            .ToArray();

        return new BrowserCommandCoverageEntryDto(
            Id: descriptor.Id,
            Aliases: descriptor.Aliases,
            Group: descriptor.Group.ToString(),
            MutationMode: descriptor.MutationMode.ToString(),
            BrowserStatus: descriptor.BrowserStatus.ToString(),
            HandlerKind: descriptor.BrowserHandlerKind.ToString(),
            UxDecision: metadata.UxDecision,
            Surface: metadata.Surface,
            FormMode: metadata.FormMode,
            PrimaryActionLabel: metadata.Label,
            PrimaryCommand: descriptor.PrimaryAlias,
            Subcommands: subcommands,
            FollowUpIssue: descriptor.FollowUpIssue,
            Reason: descriptor.Reason);
    }

    private static BrowserCommandSubcommandCoverageDto BuildSubcommand(
        ExplorerCommandDescriptor descriptor,
        ExplorerCommandSubcommandDescriptor subcommand,
        BrowserPlayerCommandCoverageMetadata metadata)
    {
        var mutationMode = ResolveSubcommandMutationMode(subcommand.BrowserStatus, descriptor.MutationMode);
        return new BrowserCommandSubcommandCoverageDto(
            Id: subcommand.Id,
            Aliases: subcommand.Aliases,
            CanonicalCommand: subcommand.CanonicalCommand,
            Group: descriptor.Group.ToString(),
            MutationMode: mutationMode.ToString(),
            BrowserStatus: subcommand.BrowserStatus.ToString(),
            HandlerKind: descriptor.BrowserHandlerKind.ToString(),
            UxDecision: ResolveSubcommandUxDecision(subcommand.BrowserStatus, metadata.Surface),
            Surface: metadata.Surface,
            FormMode: ResolveSubcommandFormMode(subcommand.BrowserStatus, metadata.Surface),
            PrimaryActionLabel: metadata.Label,
            PrimaryCommand: subcommand.CanonicalCommand,
            FollowUpIssue: subcommand.FollowUpIssue,
            Reason: subcommand.Reason);
    }

    private static string ResolveSubcommandUxDecision(
        ExplorerCommandMigrationStatus status,
        string parentSurface)
    {
        if (parentSurface == "advanced-only")
            return "advanced-diagnostics";

        return status switch
        {
            ExplorerCommandMigrationStatus.ReadOnlyParity => "contextual-button",
            ExplorerCommandMigrationStatus.MutatingParity => "guided-form",
            ExplorerCommandMigrationStatus.InteractiveFormPending => "guided-form-pending",
            ExplorerCommandMigrationStatus.StatusOnly => "status-card",
            ExplorerCommandMigrationStatus.Planned => "planned",
            ExplorerCommandMigrationStatus.Blocked => "blocked",
            ExplorerCommandMigrationStatus.ConsoleOnlyTemporarily => "console-only",
            _ => "planned"
        };
    }

    private static ExplorerCommandMutationMode ResolveSubcommandMutationMode(
        ExplorerCommandMigrationStatus status,
        ExplorerCommandMutationMode parentMutationMode) =>
        status switch
        {
            ExplorerCommandMigrationStatus.MutatingParity => ExplorerCommandMutationMode.LocalTurn,
            ExplorerCommandMigrationStatus.InteractiveFormPending => ExplorerCommandMutationMode.LocalTurn,
            ExplorerCommandMigrationStatus.ReadOnlyParity => ExplorerCommandMutationMode.ReadOnly,
            ExplorerCommandMigrationStatus.StatusOnly => ExplorerCommandMutationMode.ReadOnly,
            _ => parentMutationMode
        };

    private static string ResolveSubcommandFormMode(
        ExplorerCommandMigrationStatus status,
        string parentSurface)
    {
        if (parentSurface == "advanced-only")
            return "none";

        return status switch
        {
            ExplorerCommandMigrationStatus.MutatingParity => "guided-form",
            ExplorerCommandMigrationStatus.InteractiveFormPending => "guided-form",
            _ => "none"
        };
    }

    private static bool IsBrowserExecutable(string status) =>
        Enum.TryParse<ExplorerCommandMigrationStatus>(status, out var parsed) &&
        ExplorerCommandMigrationRegistry.IsBrowserExecutable(parsed);

    private static bool RequiresFollowUp(string status) =>
        Enum.TryParse<ExplorerCommandMigrationStatus>(status, out var parsed) &&
        parsed is not ExplorerCommandMigrationStatus.ReadOnlyParity and not ExplorerCommandMigrationStatus.MutatingParity;
}

public sealed record BrowserCommandCoverageDto(
    int SchemaVersion,
    BrowserCommandCoverageSummaryDto Summary,
    IReadOnlyList<BrowserCommandCoverageEntryDto> Commands);

public sealed record BrowserCommandCoverageSummaryDto(
    int DescriptorCount,
    int AliasCount,
    int SubcommandCount,
    int BrowserExecutableCount,
    int PlayerDefaultActionCount,
    int AdvancedOnlyActionCount,
    int MutatingCommandCount,
    int CommandsNeedingFollowUpCount);

public sealed record BrowserCommandCoverageEntryDto(
    string Id,
    IReadOnlyList<string> Aliases,
    string Group,
    string MutationMode,
    string BrowserStatus,
    string HandlerKind,
    string UxDecision,
    string Surface,
    string FormMode,
    string PrimaryActionLabel,
    string PrimaryCommand,
    IReadOnlyList<BrowserCommandSubcommandCoverageDto> Subcommands,
    string FollowUpIssue,
    string Reason);

public sealed record BrowserCommandSubcommandCoverageDto(
    string Id,
    IReadOnlyList<string> Aliases,
    string CanonicalCommand,
    string Group,
    string MutationMode,
    string BrowserStatus,
    string HandlerKind,
    string UxDecision,
    string Surface,
    string FormMode,
    string PrimaryActionLabel,
    string PrimaryCommand,
    string FollowUpIssue,
    string Reason);
