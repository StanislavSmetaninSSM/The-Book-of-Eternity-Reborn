using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ImageScenePromptPathSpellingTests
{
    // Production-used text classification only. This does not qualify native
    // Windows I/O or grant a canonical path; the admitted reader validates it.
    [Theory]
    [InlineData("Draw a portrait: scene.png", @"C:\game\game_session", true, false)]
    [InlineData("Draw a \"bright\" scene.png", @"C:\game\game_session", true, false)]
    [InlineData("CON.png", @"C:\game\game_session", true, false)]
    [InlineData(@"C:\game\game_session\scene:stream.png", @"C:\game\game_session", true, true)]
    [InlineData(@"C:\game\game_session\scene.png", @"C:\game\game_session", true, true)]
    [InlineData(@"\\?\C:\game\game_session\scene.png", @"C:\game\game_session", true, true)]
    [InlineData(@"C:\game\game_session\scene.png", @"\\?\C:\game\game_session", true, true)]
    [InlineData(@"\\?\UNC\server\share\game_session\scene.png", @"\\server\share\game_session", true, true)]
    [InlineData(@"\\server\share\game_session\scene.png", @"\\?\UNC\server\share\game_session", true, true)]
    [InlineData(@"C:\game\game_session-sibling\scene.png", @"C:\game\game_session", true, false)]
    [InlineData("/game/GAME_SESSION/scene.png", "/game/game_session", false, false)]
    [InlineData(@"/game/game_session\scene.png", "/game/game_session", false, false)]
    public void ScenePromptClassificationDoesNotValidateUnrelatedText(string path, string root, bool windows, bool expected) =>
        Assert.Equal(expected, ImageService.IsCanonicalScenePromptPathSpelling(path, root, windows));
}
