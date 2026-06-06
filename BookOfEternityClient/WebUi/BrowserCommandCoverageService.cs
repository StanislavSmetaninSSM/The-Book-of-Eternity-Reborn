using BookOfEternityClient.CommandProtocol;

namespace BookOfEternityClient.WebUi;

public static class BrowserCommandCoverageService
{
    private const int SchemaVersion = 2;
    private const string Covered = "covered";
    private const string TrackedFollowUp = "tracked-follow-up";
    private const string AdvancedOnly = "advanced-only";
    private const string Blocked = "blocked";

    private static readonly IReadOnlyDictionary<string, BrowserCommandAuditOverride> AuditOverrides =
        new Dictionary<string, BrowserCommandAuditOverride>(StringComparer.OrdinalIgnoreCase)
        {
            ["npcs"] = Tracked("#807, #817", "Read-only NPC lists, details, relationships, activities, and trade entry points are covered; start-conversation flows remain tracked interactive work."),
            ["soul_relics"] = Tracked("#802, #817", "Soul relic lists, stored/equipped state, and descriptions are covered; relic equip and unequip actions now covered by /soul_relic_equip and /soul_relic_unequip."),
            ["storage_access"] = Tracked("#814, #817", "Storage visibility and access summaries are covered; deposit and withdraw item flows remain tracked interactive work."),
            ["transport"] = Tracked("#814, #817", "Read-only transport and route summaries are covered; transport-linked storage item movement remains tracked interactive work."),
            ["interactions"] = Tracked("#807, #808, #809, #817", "Read-only interaction summaries are covered; browser-first NPC, Guardian, and resident interaction starts remain tracked interactive work."),
            ["guardians"] = Tracked("#808, #817", "Read-only Guardian state and relationship data are covered; Guardian conversation and lore actions remain tracked interactive work."),
            ["abodes"] = Tracked("#809, #817", "Read-only Abode state is covered; resident conversation, history, and transfer actions remain tracked interactive work."),
            ["shining_abode"] = Tracked("#811, #812, #817", "Read-only Shining Abode state is covered; broader faction/project and incarnation-gate actions remain tracked interactive work."),
            ["shining_politics"] = Tracked("#810, #817", "Read-only Shining politics data is covered; founding, regrouping, and leadership actions remain tracked interactive work."),
            ["shining_treasury"] = Tracked("#811, #813, #817", "Treasury browser parity is covered; broader Shining project and relic-forge flows remain tracked interactive work."),
            ["afterlife_archive"] = Tracked("#816, #817", "Read-only afterlife archive data is covered; consultation, project fuel, and direct pull actions remain tracked interactive work."),
            ["feathers"] = Tracked("#815, #817", "Ink Feather totals and related soul data are covered; fate reveal and rewrite actions remain tracked interactive work.")
        };

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
            CommandsNeedingFollowUpCount: commands.Count(static command => command.AuditStatus == TrackedFollowUp || RequiresFollowUp(command.BrowserStatus)));

        return new BrowserCommandCoverageDto(SchemaVersion, summary, commands);
    }

    private static BrowserCommandCoverageEntryDto BuildEntry(ExplorerCommandDescriptor descriptor)
    {
        var metadata = BrowserPlayerCommandMenuBuilder.GetCoverageMetadata(descriptor);
        var audit = BuildAuditMetadata(descriptor, metadata);
        var subcommands = descriptor.SubcommandDescriptors
            .Select(subcommand => BuildSubcommand(descriptor, subcommand, metadata, audit))
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
            FollowUpIssue: FirstNonEmpty(audit.FollowUpIssue, descriptor.FollowUpIssue),
            Reason: FirstNonEmpty(audit.Reason, descriptor.Reason),
            AuditStatus: audit.AuditStatus,
            SampleDataStatus: audit.SampleDataStatus,
            BrowserEvidence: audit.BrowserEvidence,
            ConsoleEvidence: audit.ConsoleEvidence,
            ParityNotes: audit.ParityNotes,
            ReadabilityNotes: audit.ReadabilityNotes,
            GapSummary: audit.GapSummary);
    }

    private static BrowserCommandSubcommandCoverageDto BuildSubcommand(
        ExplorerCommandDescriptor descriptor,
        ExplorerCommandSubcommandDescriptor subcommand,
        BrowserPlayerCommandCoverageMetadata metadata,
        BrowserCommandAuditMetadata parentAudit)
    {
        var mutationMode = ResolveSubcommandMutationMode(subcommand.BrowserStatus, descriptor.MutationMode);
        var audit = BuildSubcommandAuditMetadata(descriptor, subcommand, metadata, parentAudit, mutationMode);
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
            FollowUpIssue: FirstNonEmpty(audit.FollowUpIssue, subcommand.FollowUpIssue, parentAudit.FollowUpIssue),
            Reason: FirstNonEmpty(audit.Reason, subcommand.Reason, parentAudit.Reason),
            AuditStatus: audit.AuditStatus,
            SampleDataStatus: audit.SampleDataStatus,
            BrowserEvidence: audit.BrowserEvidence,
            ConsoleEvidence: audit.ConsoleEvidence,
            ParityNotes: audit.ParityNotes,
            ReadabilityNotes: audit.ReadabilityNotes,
            GapSummary: audit.GapSummary);
    }

    private static BrowserCommandAuditMetadata BuildAuditMetadata(
        ExplorerCommandDescriptor descriptor,
        BrowserPlayerCommandCoverageMetadata metadata)
    {
        var status = ResolveAuditStatus(descriptor.BrowserStatus, metadata.Surface);
        var isMutating = descriptor.MutationMode == ExplorerCommandMutationMode.LocalTurn;
        var audit = new BrowserCommandAuditMetadata(
            AuditStatus: status,
            SampleDataStatus: isMutating
                ? "Command-coverage fixture records this row; seeded local-turn prompt fixtures exercise the C# DTO/prompt family for this command group."
                : "Command-coverage fixture records this row; seeded read-only command fixtures exercise the C# DTO builder family for this command group.",
            BrowserEvidence: $"Browser uses the C# {descriptor.BrowserHandlerKind} handler through /api/explorer/command and exposes {metadata.Surface}/{metadata.FormMode} coverage metadata.",
            ConsoleEvidence: "Console command execution remains the C# gameplay authority; browser output reuses ExplorerCommandResult DTOs instead of TypeScript gameplay logic.",
            ParityNotes: BuildParityNotes(status, descriptor.BrowserStatus, isMutating),
            ReadabilityNotes: isMutating
                ? "Browser presents a guided prompt form and local-write status before submit; resulting blocks render as typed tables, lists, key-value grids, maps, images, or advanced raw details."
                : "Browser renders typed tables, lists, key-value grids, maps, and images where available; raw diagnostic JSON is not the only player surface.",
            GapSummary: BuildGapSummary(status));

        if (AuditOverrides.TryGetValue(descriptor.Id, out var auditOverride))
        {
            audit = audit with
            {
                AuditStatus = auditOverride.AuditStatus,
                FollowUpIssue = auditOverride.FollowUpIssue,
                Reason = auditOverride.GapSummary,
                GapSummary = auditOverride.GapSummary,
                ParityNotes = "Current browser read/prompt parity is documented; remaining interactive scope is tracked separately."
            };
        }

        return audit;
    }

    private static BrowserCommandAuditMetadata BuildSubcommandAuditMetadata(
        ExplorerCommandDescriptor descriptor,
        ExplorerCommandSubcommandDescriptor subcommand,
        BrowserPlayerCommandCoverageMetadata metadata,
        BrowserCommandAuditMetadata parentAudit,
        ExplorerCommandMutationMode mutationMode)
    {
        var status = parentAudit.AuditStatus == AdvancedOnly
            ? AdvancedOnly
            : ResolveAuditStatus(subcommand.BrowserStatus, metadata.Surface);
        var isMutating = mutationMode == ExplorerCommandMutationMode.LocalTurn;
        var followUp = FirstNonEmpty(subcommand.FollowUpIssue, parentAudit.FollowUpIssue);
        var reason = FirstNonEmpty(subcommand.Reason, parentAudit.Reason);
        if (!string.IsNullOrWhiteSpace(followUp) && status == Covered)
            status = TrackedFollowUp;

        return new BrowserCommandAuditMetadata(
            AuditStatus: status,
            SampleDataStatus: "Command-coverage fixture records this subcommand row; parent command fixtures exercise the alias and canonical-command path.",
            BrowserEvidence: $"Browser executes canonical subcommand {subcommand.CanonicalCommand} through the parent {descriptor.Id} C# web command handler.",
            ConsoleEvidence: "Console parser and renderer remain the C# authority for the same canonical subcommand.",
            ParityNotes: BuildParityNotes(status, subcommand.BrowserStatus, isMutating),
            ReadabilityNotes: isMutating
                ? "Browser keeps the subcommand behind the same guided form/local-write safety path as the parent command."
                : "Browser keeps the subcommand result in the same typed block renderer as the parent command.",
            GapSummary: string.IsNullOrWhiteSpace(reason) ? BuildGapSummary(status) : reason,
            FollowUpIssue: followUp,
            Reason: reason);
    }

    private static string ResolveAuditStatus(ExplorerCommandMigrationStatus status, string surface)
    {
        if (surface == "advanced-only")
            return AdvancedOnly;

        return status switch
        {
            ExplorerCommandMigrationStatus.ReadOnlyParity => Covered,
            ExplorerCommandMigrationStatus.MutatingParity => Covered,
            ExplorerCommandMigrationStatus.InteractiveFormPending => TrackedFollowUp,
            ExplorerCommandMigrationStatus.StatusOnly => TrackedFollowUp,
            ExplorerCommandMigrationStatus.Planned => TrackedFollowUp,
            ExplorerCommandMigrationStatus.ConsoleOnlyTemporarily => TrackedFollowUp,
            ExplorerCommandMigrationStatus.Blocked => Blocked,
            _ => TrackedFollowUp
        };
    }

    private static string BuildParityNotes(string auditStatus, ExplorerCommandMigrationStatus browserStatus, bool isMutating) =>
        auditStatus switch
        {
            Covered when isMutating => "Browser prompt flow is expected to match console command data and local-write validation for the audited command scope.",
            Covered => "Browser read surface is expected to be no worse than console output for player-visible data in the audited command scope.",
            AdvancedOnly => "Command is intentionally available only through explicit advanced diagnostics, not the default player UI.",
            Blocked => "Browser execution is blocked until the referenced command migration work is complete.",
            _ => $"Browser status is {browserStatus}; current parity is documented and remaining scope is tracked by follow-up issue."
        };

    private static string BuildGapSummary(string auditStatus) =>
        auditStatus switch
        {
            Covered => "No tracked browser parity gap for the command scope audited in #804.",
            AdvancedOnly => "Advanced-only by design; hidden from the default player UI and available through explicit diagnostics.",
            Blocked => "Blocked in browser; keep the console path as authority until the follow-up issue removes the blocker.",
            _ => "Follow-up issue tracks remaining browser interaction beyond the current readable or prompt-backed surface."
        };

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

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

    private static BrowserCommandAuditOverride Tracked(string followUpIssue, string gapSummary) =>
        new(TrackedFollowUp, followUpIssue, gapSummary);
}

internal sealed record BrowserCommandAuditMetadata(
    string AuditStatus,
    string SampleDataStatus,
    string BrowserEvidence,
    string ConsoleEvidence,
    string ParityNotes,
    string ReadabilityNotes,
    string GapSummary,
    string FollowUpIssue = "",
    string Reason = "");

internal sealed record BrowserCommandAuditOverride(
    string AuditStatus,
    string FollowUpIssue,
    string GapSummary);

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
    string Reason,
    string AuditStatus,
    string SampleDataStatus,
    string BrowserEvidence,
    string ConsoleEvidence,
    string ParityNotes,
    string ReadabilityNotes,
    string GapSummary);

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
    string Reason,
    string AuditStatus,
    string SampleDataStatus,
    string BrowserEvidence,
    string ConsoleEvidence,
    string ParityNotes,
    string ReadabilityNotes,
    string GapSummary);
