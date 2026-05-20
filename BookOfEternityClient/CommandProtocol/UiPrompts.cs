using System.Text.Json.Serialization;

namespace BookOfEternityClient.CommandProtocol;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(UiConfirmationPrompt), "confirmation")]
[JsonDerivedType(typeof(UiSelectionPrompt), "selection")]
[JsonDerivedType(typeof(UiTextInputPrompt), "textInput")]
[JsonDerivedType(typeof(UiLongTextInputPrompt), "longTextInput")]
public abstract class UiPrompt
{
    public string Id { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public bool Required { get; init; }
}

public sealed class UiConfirmationPrompt : UiPrompt
{
    public bool DefaultValue { get; init; }
}

public sealed class UiSelectionPrompt : UiPrompt
{
    public List<UiSelectionOption> Options { get; init; } = [];
    public bool AllowCustom { get; init; }
}

public sealed class UiSelectionOption
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool Disabled { get; init; }
}

public sealed class UiTextInputPrompt : UiPrompt
{
    public string DefaultValue { get; init; } = string.Empty;
    public string Placeholder { get; init; } = string.Empty;
}

public sealed class UiLongTextInputPrompt : UiPrompt
{
    public string DefaultValue { get; init; } = string.Empty;
    public string Placeholder { get; init; } = string.Empty;
    public int? MinLines { get; init; }
    public int? MaxLines { get; init; }
}
