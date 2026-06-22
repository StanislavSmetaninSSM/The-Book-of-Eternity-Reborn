using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.CommandProtocol;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(UiTextBlock), "text")]
[JsonDerivedType(typeof(UiPanelBlock), "panel")]
[JsonDerivedType(typeof(UiEntityDossierBlock), "entityDossier")]
[JsonDerivedType(typeof(UiTableBlock), "table")]
[JsonDerivedType(typeof(UiListBlock), "list")]
[JsonDerivedType(typeof(UiKeyValueGridBlock), "keyValueGrid")]
[JsonDerivedType(typeof(UiMessageBlock), "message")]
[JsonDerivedType(typeof(UiRawJsonBlock), "rawJson")]
[JsonDerivedType(typeof(UiImageBlock), "image")]
[JsonDerivedType(typeof(UiMapBlock), "map")]
public abstract class UiBlock
{
}

public sealed class UiTextBlock : UiBlock
{
    public string Text { get; init; } = string.Empty;
    public UiTone Tone { get; init; } = UiTone.Default;
}

public sealed class UiPanelBlock : UiBlock
{
    public string Title { get; init; } = string.Empty;
    public List<UiBlock> Blocks { get; init; } = [];
}

public sealed class UiEntityDossierBlock : UiBlock
{
    public string EntityType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public List<UiEntityBadge> Badges { get; init; } = [];
    public UiEntityMedia? Media { get; init; }
    public List<UiEntityDossierSection> Sections { get; init; } = [];
}

public sealed class UiEntityBadge
{
    public string Label { get; init; } = string.Empty;
    public UiTone Tone { get; init; } = UiTone.Default;
    public string Icon { get; init; } = string.Empty;
}

public sealed class UiEntityMedia
{
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string MediaId { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public string AltText { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long Length { get; init; }
    public DateTimeOffset ModifiedAtUtc { get; init; }
}

public sealed class UiEntityDossierSection
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public bool Collapsible { get; init; }
    public bool InitiallyExpanded { get; init; } = true;
    public List<UiBlock> Blocks { get; init; } = [];
}

public sealed class UiTableBlock : UiBlock
{
    public string Title { get; init; } = string.Empty;
    public List<string> Columns { get; init; } = [];
    public List<UiTableRow> Rows { get; init; } = [];
}

public sealed class UiTableRow
{
    public List<string> Cells { get; init; } = [];
}

public sealed class UiListBlock : UiBlock
{
    public bool Ordered { get; init; }
    public List<string> Items { get; init; } = [];
}

public sealed class UiKeyValueGridBlock : UiBlock
{
    public List<UiKeyValueItem> Items { get; init; } = [];
}

public sealed class UiKeyValueItem
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public sealed class UiMessageBlock : UiBlock
{
    public UiNotificationSeverity Severity { get; init; } = UiNotificationSeverity.Info;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class UiRawJsonBlock : UiBlock
{
    public string Title { get; init; } = string.Empty;
    public JsonNode? Json { get; init; }
}

public sealed class UiImageBlock : UiBlock
{
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string MediaId { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public string AltText { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long Length { get; init; }
    public DateTimeOffset ModifiedAtUtc { get; init; }
}

public sealed class UiMapBlock : UiBlock
{
    public string Title { get; init; } = string.Empty;
    public MapViewDto Map { get; init; } = new();
}

public sealed class MapViewDto
{
    public int SchemaVersion { get; init; } = 1;
    public string Realm { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string CurrentNodeId { get; init; } = string.Empty;
    public List<MapLayerDto> Layers { get; init; } = [];
    public List<MapZLevelDto> ZLevels { get; init; } = [];
    public List<MapNodeDto> Nodes { get; init; } = [];
    public List<MapLinkDto> Links { get; init; } = [];
    public List<MapRegionDto> Regions { get; init; } = [];
}

public sealed class MapLayerDto
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
}

public sealed class MapZLevelDto
{
    public int Z { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class MapNodeDto
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public double X { get; init; }
    public double Y { get; init; }
    public int Z { get; init; }
    public string Layer { get; init; } = "world";
    public bool IsCurrent { get; init; }
    public string OwnerFactionId { get; init; } = string.Empty;
    public string OwnerFactionName { get; init; } = string.Empty;
    public Dictionary<string, int> Influence { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<MapDetailItemDto> Details { get; init; } = [];
    public bool IsPlaceholder { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
    public string ImageAltText { get; init; } = string.Empty;
}

public sealed class MapLinkDto
{
    public string Id { get; init; } = string.Empty;
    public string SourceNodeId { get; init; } = string.Empty;
    public string TargetNodeId { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string Layer { get; init; } = "world";
    public int? Z { get; init; }
}

public sealed class MapRegionDto
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string OwnerFactionId { get; init; } = string.Empty;
    public string OwnerFactionName { get; init; } = string.Empty;
    public string Layer { get; init; } = "world";
    public int? Z { get; init; }
    public List<string> NodeIds { get; init; } = [];
}

public sealed class MapDetailItemDto
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UiTone
{
    Default,
    Muted,
    Subtle,
    Accent,
    Success,
    Warning,
    Error
}
