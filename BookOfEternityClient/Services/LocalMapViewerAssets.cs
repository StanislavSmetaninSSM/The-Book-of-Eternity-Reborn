namespace BookOfEternityClient.Services;

/// <summary>
/// Provides the unified map viewer bundle to both the standalone
/// <c>map_viewer.html</c> (written by the console <c>/map</c> command) and the
/// local web UI shell (served at <c>/assets/map-viewer.js</c>).
/// </summary>
/// <remarks>
/// <para>
/// The bundle is a single self-contained IIFE produced by
/// <c>BookOfEternityClient.WebFrontend</c>'s <c>npm run build:map-viewer</c>
/// (see <c>scripts/build-map-viewer.mjs</c> and
/// <c>vite.map-viewer.config.ts</c>) and embedded as a resource
/// (<c>Assets/MapViewer/map-viewer-bundle.js</c>) so that
/// <c>dotnet build</c>/<c>dotnet test</c> never depend on Node.
/// </para>
/// <para>
/// It mounts the SAME React <c>MapAtlas</c> component the embedded browser
/// client uses (via <c>BlockRenderer</c>), guaranteeing one renderer for all
/// three map surfaces (Vite React client, standalone viewer, local web shell).
/// The shell calls <c>window.BookOfEternityMap.mount(root, map)</c> in place of
/// the old <c>BookOfEternityMapViewer.renderMapBlock</c>.
/// </para>
/// </remarks>
public static class LocalMapViewerAssets
{
    /// <summary>
    /// Logical resource name as expected by <see cref="System.Reflection.Assembly"/>,
    /// using the assembly's default namespace (<c>BookOfEternityClient</c>) plus the
    /// on-disk path of the embedded file.
    /// </summary>
    public const string BundleResourceName = "BookOfEternityClient.Assets.MapViewer.map-viewer-bundle.js";

    /// <summary>
    /// JavaScript global exposed by the bundle. The local web UI shell and the
    /// standalone HTML both call <c>window.&lt;Global&gt;.mount(root, map)</c>.
    /// </summary>
    public const string Global = "BookOfEternityMap";

    private static readonly Lazy<string> BundleLazy = new(LoadBundle);

    /// <summary>
    /// The full map viewer bundle source (React + MapAtlas + inlined CSS).
    /// Cached for the process lifetime.
    /// </summary>
    public static string Bundle => BundleLazy.Value;

    private static string LoadBundle()
    {
        var assembly = typeof(LocalMapViewerAssets).Assembly;
        using var stream = assembly.GetManifestResourceStream(BundleResourceName);
        if (stream is null)
        {
            throw new InvalidOperationException(
                $"Embedded map viewer bundle '{BundleResourceName}' was not found. " +
                "Run `npm run build:map-viewer` from BookOfEternityClient.WebFrontend " +
                "and commit BookOfEternityClient/Assets/MapViewer/map-viewer-bundle.js.");
        }
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
