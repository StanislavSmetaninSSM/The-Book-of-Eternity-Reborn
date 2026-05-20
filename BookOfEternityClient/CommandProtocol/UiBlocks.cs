using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.CommandProtocol;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(UiTextBlock), "text")]
[JsonDerivedType(typeof(UiPanelBlock), "panel")]
[JsonDerivedType(typeof(UiTableBlock), "table")]
[JsonDerivedType(typeof(UiListBlock), "list")]
[JsonDerivedType(typeof(UiKeyValueGridBlock), "keyValueGrid")]
[JsonDerivedType(typeof(UiMessageBlock), "message")]
[JsonDerivedType(typeof(UiRawJsonBlock), "rawJson")]
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
