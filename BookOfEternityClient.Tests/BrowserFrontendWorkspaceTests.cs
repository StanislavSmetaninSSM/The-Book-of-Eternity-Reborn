using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class BrowserFrontendWorkspaceTests
{
    private static readonly string RepoRoot = TestRepoPaths.RepoRoot;
    private static readonly string FrontendRoot = Path.Combine(RepoRoot, "BookOfEternityClient.WebFrontend");

    [Fact]
    public void FrontendWorkspace_HasViteReactTypeScriptPackageContract()
    {
        var packageJsonPath = Path.Combine(FrontendRoot, "package.json");
        Assert.True(File.Exists(packageJsonPath), $"Missing {packageJsonPath}");

        using var document = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
        var root = document.RootElement;

        Assert.Equal("book-of-eternity-reborn-webfrontend", root.GetProperty("name").GetString());
        Assert.True(root.GetProperty("private").GetBoolean());
        Assert.Equal("module", root.GetProperty("type").GetString());

        var scripts = root.GetProperty("scripts");
        Assert.Equal("vite --host 127.0.0.1", scripts.GetProperty("dev").GetString());
        Assert.Equal("tsc --noEmit -p tsconfig.app.json && tsc --noEmit -p tsconfig.node.json", scripts.GetProperty("typecheck").GetString());
        Assert.Equal("npm run typecheck && vite build", scripts.GetProperty("build").GetString());
        Assert.Equal("vite preview --host 127.0.0.1", scripts.GetProperty("preview").GetString());

        var dependencies = root.GetProperty("dependencies");
        Assert.True(dependencies.TryGetProperty("react", out _));
        Assert.True(dependencies.TryGetProperty("react-dom", out _));

        var devDependencies = root.GetProperty("devDependencies");
        Assert.True(devDependencies.TryGetProperty("@vitejs/plugin-react", out _));
        Assert.True(devDependencies.TryGetProperty("vite", out _));
        Assert.True(devDependencies.TryGetProperty("typescript", out _));
        Assert.True(devDependencies.TryGetProperty("@types/react", out _));
        Assert.True(devDependencies.TryGetProperty("@types/react-dom", out _));
    }

    [Fact]
    public void FrontendWorkspace_HasStrictViteTypeScriptStructure()
    {
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "index.html")));
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "vite.config.ts")));
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "tsconfig.json")));
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "tsconfig.app.json")));
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "tsconfig.node.json")));
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "src", "main.tsx")));
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "src", "App.tsx")));
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "src", "styles.css")));
        Assert.True(File.Exists(Path.Combine(FrontendRoot, "src", "vite-env.d.ts")));

        var appConfig = File.ReadAllText(Path.Combine(FrontendRoot, "tsconfig.app.json"));
        Assert.Contains("\"strict\": true", appConfig, StringComparison.Ordinal);
        Assert.Contains("\"jsx\": \"react-jsx\"", appConfig, StringComparison.Ordinal);

        var viteConfig = File.ReadAllText(Path.Combine(FrontendRoot, "vite.config.ts"));
        Assert.Contains("@vitejs/plugin-react", viteConfig, StringComparison.Ordinal);
        Assert.Contains("outDir: 'dist'", viteConfig, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontendWorkspace_IgnoresGeneratedNodeArtifacts()
    {
        var gitignore = File.ReadAllText(Path.Combine(RepoRoot, ".gitignore"));

        Assert.Contains("/BookOfEternityClient.WebFrontend/node_modules/", gitignore, StringComparison.Ordinal);
        Assert.Contains("/BookOfEternityClient.WebFrontend/dist/", gitignore, StringComparison.Ordinal);
        Assert.Contains("/BookOfEternityClient.WebFrontend/.vite/", gitignore, StringComparison.Ordinal);
        Assert.Contains("/BookOfEternityClient.WebFrontend/*.tsbuildinfo", gitignore, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontendWorkspace_DocumentsCSharpAuthorityAndHostHandoff()
    {
        var readme = File.ReadAllText(Path.Combine(FrontendRoot, "README.md"));
        var hostDoc = File.ReadAllText(Path.Combine(RepoRoot, "docs", "web-ui", "local-web-host.md"));
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));

        Assert.Contains("npm install", readme, StringComparison.Ordinal);
        Assert.Contains("npm run dev", readme, StringComparison.Ordinal);
        Assert.Contains("npm run typecheck", readme, StringComparison.Ordinal);
        Assert.Contains("npm run build", readme, StringComparison.Ordinal);
        Assert.Contains("C# runtime remains the authority", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("issue #702", readme, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("BookOfEternityClient.WebFrontend", hostDoc, StringComparison.Ordinal);
        Assert.Contains("npm run build", hostDoc, StringComparison.Ordinal);
        Assert.Contains("#702", hostDoc, StringComparison.Ordinal);

        Assert.Contains("C# API остаётся источником истины", app, StringComparison.Ordinal);
        Assert.DoesNotContain("debug dashboard", app, StringComparison.OrdinalIgnoreCase);
    }
}
