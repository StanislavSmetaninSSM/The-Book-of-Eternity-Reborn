using BookOfEternityClient.UI;

namespace BookOfEternityClient.Services;

internal sealed record WoundPlayerFact(string Label, string Value);
internal sealed record WoundPlayerMethod(string Name, IReadOnlyList<string> Requirements, string Policy);
internal sealed record WoundPlayerEntry(
    string Name, string Severity, string Description, IReadOnlyList<WoundPlayerFact> Facts,
    IReadOnlyList<WoundPlayerMethod> Methods, IReadOnlyList<string> Diagnosis);
internal sealed record WoundPlayerHistoryEntry(string Name, string Severity, string Summary);
internal sealed record WoundPlayerProjectionResult(
    bool IsAvailable, string StatusMessage, IReadOnlyList<WoundPlayerEntry> Active,
    IReadOnlyList<WoundPlayerHistoryEntry> History);
internal sealed record WoundPlayerProjectionInput(
    WoundCarrierCatalogInput Carriers, WoundIdentityState Identities, WoundHistoryState History,
    EffectMechanicsSnapshot Effects, string Realm, ResourceDefinitionCatalog Resources,
    IReadOnlyDictionary<string, string> KnownItemNames, bool SelfVisible = true);

/// <summary>Display-only self projection. No command, quote or mutation authority.</summary>
internal static partial class WoundPlayerProjection
{
    internal const string UnavailableMessage = "Сейчас невозможно надёжно определить состояние ран.";
    internal static WoundPlayerProjectionResult Unavailable() => new(false, UnavailableMessage, [], []);
    internal static WoundPlayerProjectionResult Build(WoundPlayerProjectionInput input) => Unavailable();
}
