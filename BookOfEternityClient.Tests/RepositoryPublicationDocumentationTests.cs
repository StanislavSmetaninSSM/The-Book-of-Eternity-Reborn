using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class RepositoryPublicationDocumentationTests
{
    private static string RepositoryRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", ".."));

    private static string Read(string relativePath)
    {
        var path = Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Required publication file is missing: {relativePath}");
        return File.ReadAllText(path, Encoding.UTF8);
    }

    private static string Sha256(string relativePath)
    {
        var path = Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }

    [Fact]
    public void RootPublicationDocuments_DefineApprovedMixedLicenseBoundary()
    {
        var readme = Read("README.md");
        var content = Read("CONTENT_LICENSE.md");
        var notices = Read("THIRD_PARTY_NOTICES.md");
        var license = Read("LICENSE");

        Assert.StartsWith("GNU AFFERO GENERAL PUBLIC LICENSE", license.TrimStart());
        Assert.Equal(
            "0D96A4FF68AD6D4B6F1F30F713B18D5184912BA8DD389F86AA7710DB079ABCB0",
            Sha256("LICENSE"));
        Assert.Contains("AGPL-3.0-or-later", readme, StringComparison.Ordinal);
        Assert.Contains("AGPL-3.0-or-later", content, StringComparison.Ordinal);
        Assert.Contains("CC BY-NC-SA 4.0", content, StringComparison.Ordinal);
        Assert.Contains("Copyright © 2026 Stanislav Smetanin (Lottarend)", content, StringComparison.Ordinal);
        Assert.Contains("unreleased", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("non-commercial", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Игра ещё не вышла", readme, StringComparison.Ordinal);
        Assert.Contains("Совместимость сохранений не является требованием", readme, StringComparison.Ordinal);
        Assert.Contains("Suno Basic", notices, StringComparison.Ordinal);
        Assert.Contains("not licensed under", notices, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RepositoryGovernanceFiles_RequireOwnerReviewedPullRequests()
    {
        Assert.Equal("* @StanislavSmetaninSSM\n", Read(".github/CODEOWNERS").Replace("\r\n", "\n"));

        var contributing = Read("CONTRIBUTING.md");
        Assert.Contains("tracked GitHub Issue", contributing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pull request", contributing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@StanislavSmetaninSSM", contributing, StringComparison.Ordinal);
        Assert.Contains("AGPL-3.0-or-later", contributing, StringComparison.Ordinal);
        Assert.Contains("CC BY-NC-SA 4.0", contributing, StringComparison.Ordinal);
        Assert.Contains("Third-party/excluded assets are not covered by either blanket grant", contributing, StringComparison.Ordinal);
        Assert.Contains("appropriate rights, provenance, and required notices", contributing, StringComparison.Ordinal);

        var template = Read(".github/pull_request_template.md");
        Assert.Contains("Tracked issue", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Verification", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("License / asset impact", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GM / documentation impact", template, StringComparison.OrdinalIgnoreCase);

        var issuePolicy = Read(".github/ISSUE_TRACKING.md");
        Assert.Contains("Collaborators only", issuePolicy, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("publicly readable", issuePolicy, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AssetNotices_PreserveMusicAndDocumentProvenance()
    {
        var music = Read("BookOfEternityClient/Music/README.md");
        Assert.Contains("Suno Basic", music, StringComparison.Ordinal);
        Assert.Contains("non-commercial", music, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not licensed under", music, StringComparison.OrdinalIgnoreCase);

        var sounds = Read("BookOfEternityClient/Sounds/README.md");
        Assert.Contains("https://freesound.org/s/833055/", sounds, StringComparison.Ordinal);
        Assert.Contains("https://freesound.org/s/810754/", sounds, StringComparison.Ordinal);
        Assert.Contains("https://freesound.org/s/810739/", sounds, StringComparison.Ordinal);
        Assert.Contains("https://freesound.org/s/810748/", sounds, StringComparison.Ordinal);
        Assert.Contains("scripts/generate-notification-sound.ps1", sounds, StringComparison.Ordinal);

        var generatedArt = Read("BookOfEternityClient.WebFrontend/public/generated-art/README.md");
        Assert.Contains("game-shell-bg.png", generatedArt, StringComparison.Ordinal);
        Assert.Contains("launcher-side-left.png", generatedArt, StringComparison.Ordinal);
        Assert.Contains("launcher-side-right.png", generatedArt, StringComparison.Ordinal);
    }

    [Fact]
    public void NotificationSound_IsDeterministicOriginalAsset()
    {
        Assert.Equal(
            "72DF5E7044E57595EB57A5F38DE756A64959C579C15B3E11065F2598348E21AF",
            Sha256("BookOfEternityClient/Sounds/sound-notification.wav"));
    }

    [Fact]
    public void RevokedGenerationCredential_IsAbsentFromCurrentDocumentation()
    {
        var historicalPlan = Read("docs/superpowers/plans/2026-05-30-browser-animations-art-terms.md");
        Assert.DoesNotMatch(new Regex(@"plln_[A-Za-z0-9_-]{20,}", RegexOptions.CultureInvariant), historicalPlan);
        Assert.Contains("POLLINATIONS_API_KEY", historicalPlan, StringComparison.Ordinal);
    }
}
