namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private readonly SpiritualOriginalDraftInputs? _sameTurnOwnerInputs;

    /// <summary>
    /// Reads current same-turn owner data from the bound original draft, when present.
    /// Signed pre-turn snapshots and control files continue to use the real filesystem.
    /// </summary>
    /// <param name="path">
    /// Exact registered draft path to read.
    /// </param>
    /// <returns>
    /// The retained current text, including a present empty file, or <see langword="null"/>
    /// when the registered file was absent.
    /// </returns>
    private Task<string?> ReadSameTurnOwnerCurrentTextAsync(string path) =>
        _sameTurnOwnerInputs is { } inputs
            ? Task.FromResult(inputs.ReadText(path))
            : _fs.ReadFileAsync(path);

    /// <summary>
    /// Checks current owner presence in the same bound read view used for its contents.
    /// </summary>
    /// <param name="path">
    /// Exact registered draft path to inspect.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the current owner file exists in the selected view;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private bool SameTurnOwnerCurrentFileExists(string path) =>
        _sameTurnOwnerInputs is { } inputs
            ? inputs.ReadImage(path).Existed
            : _fs.FileExists(path);
}
