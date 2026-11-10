using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ImageExportPathSpellingTests
{
    // Pure pathname policy only: these cases do not qualify native Windows I/O.
    [Theory]
    [InlineData(@"C:\game\game_session\copy.png", @"C:\game\game_session", true, true)]
    [InlineData(@"\\?\C:\game\game_session\copy.png", @"C:\game\game_session", true, true)]
    [InlineData(@"C:\game\game_session\copy.png", @"\\?\C:\game\game_session", true, true)]
    [InlineData(@"\\?\UNC\server\share\game_session\copy.png", @"\\server\share\game_session", true, true)]
    [InlineData(@"\\server\share\game_session\copy.png", @"\\?\UNC\server\share\game_session", true, true)]
    [InlineData(@"C:\game\game_session-outside\copy.png", @"C:\game\game_session", true, false)]
    [InlineData("/game/GAME_SESSION/copy.png", "/game/game_session", false, false)]
    [InlineData(@"/game/game_session\copy.png", "/game/game_session", false, false)]
    public void CanonicalExportExclusionUsesHostPathSpelling(string path, string root, bool windows, bool expected) =>
        Assert.Equal(expected, ImageService.IsCanonicalExportPathSpelling(path, root, windows));
}
