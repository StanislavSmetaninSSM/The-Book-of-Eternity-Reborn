using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class ConsoleAudioLinuxTests
{
    [Fact]
    public async Task OwnedFixtureParentTimeout_StopsAndReapsOnlyOriginalHost()
    {
        using var f = new AudioLinuxFixture();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Run("fixture-cleanup", TimeSpan.FromMilliseconds(500)));
        Assert.True(f.OwnedCleanupConfirmed, "Timed out original host must be stopped/reaped before cleanup success.");
    }

    [Fact]
    public async Task ConcurrentPlaylistStopSettingsAndDispose_DoNotOverlapOriginalMusic()
    {
        using var f = new AudioLinuxFixture(); var r = await f.Run("concurrency");
        Assert.False(r.GetProperty("HadOverlap").GetBoolean());
        Assert.Equal(1, r.GetProperty("Active").GetInt32());
        Assert.DoesNotContain("Main Theme", r.GetProperty("Last").GetString());
        Assert.True(r.GetProperty("AllDisposed").GetBoolean());
        Assert.Equal("Disposed", r.GetProperty("Outcome").GetString());
    }

    [Fact]
    public async Task MissingAssetsAndMutedRequests_NeverCreateOutput()
    {
        using var f = new AudioLinuxFixture(); var r = await f.Run("no-assets");
        Assert.Equal("NoAssets", r.GetProperty("Music").GetString()); Assert.Equal("NoAssets", r.GetProperty("Cue").GetString());
        Assert.Equal("Muted", r.GetProperty("Muted").GetString()); Assert.Equal(0, r.GetProperty("Count").GetInt32());
    }

    [Fact]
    public async Task SoundSettingsDisableAndZeroVolume_StopAlreadyActiveCues()
    {
        using var f = new AudioLinuxFixture(); var r = await f.Run("sound-settings");
        Assert.True(r.GetProperty("Disabled").GetBoolean()); Assert.True(r.GetProperty("ZeroVolumeDisposed").GetBoolean());
        Assert.Equal(2, r.GetProperty("Canceled").GetInt32()); Assert.Equal(2, r.GetProperty("Count").GetInt32());
    }

    [Fact]
    public async Task ActualMainMenuLayoutsAndSettings_ShowSafeCapabilityMessage()
    {
        using var f = new AudioLinuxFixture(); var r = await f.Run("renderer");
        Assert.Contains("Аудио недоступно", r.GetProperty("Screen").GetString());
        Assert.Contains(r.GetProperty("SettingsLabels").EnumerateArray(), e => e.GetString()!.Contains("Аудио недоступно"));
    }
    [Fact]
    public async Task PublicService_SelectsLinuxAndReportsUnavailableWithoutWindowsOrDeviceCalls()
    {
        using var fixture = new AudioLinuxFixture(); var report = await fixture.Run("console");
        Assert.Equal(0, report.GetProperty("WindowsAttempts").GetInt32());
        Assert.Equal("BackendUnavailable", report.GetProperty("Outcome").GetString());
        Assert.True(report.GetProperty("SdlAttempts").GetInt32() > 0);
        Assert.Equal(0, report.GetProperty("DefaultDeviceCalls").GetInt32());
    }

    [Fact]
    public async Task AllRealCues_StopAllMuteThrottleAndDisposeOwnEverySession()
    {
        using var f = new AudioLinuxFixture(); var r = await f.Run("cues");
        Assert.Equal("Stopped", r.GetProperty("Stopped").GetString());
        Assert.Equal("Muted", r.GetProperty("Muted").GetString());
        Assert.Equal(5, r.GetProperty("MutedCount").GetInt32());
        Assert.Equal(5, r.GetProperty("Canceled").GetInt32());
        Assert.Equal(6, r.GetProperty("FinalCount").GetInt32());
        Assert.Equal(6, r.GetProperty("Disposed").GetInt32());
        Assert.Equal("Disposed", r.GetProperty("Outcome").GetString());
    }

    [Fact]
    public async Task OriginalMusic_NonrepeatContextVolumeAndMuteDoNotReopenCurrentTrack()
    {
        using var f = new AudioLinuxFixture(); var r = await f.Run("music");
        Assert.Equal(1, r.GetProperty("SameCount").GetInt32());
        Assert.NotEqual(r.GetProperty("First").GetString(), r.GetProperty("Second").GetString());
        Assert.StartsWith("Main Theme", r.GetProperty("First").GetString());
        Assert.DoesNotContain("Main Theme", r.GetProperty("Game").GetString());
        Assert.True(r.GetProperty("PreviousDisposedBeforeGame").GetBoolean());
        Assert.Equal(1, r.GetProperty("Clamped").GetSingle());
        Assert.True(r.GetProperty("AllDisposed").GetBoolean());
    }

    [Theory]
    [InlineData("debt", 2, "Playing")]
    [InlineData("dispose-debt", 1, "Disposed")]
    public async Task StopTimeout_RetainsOriginalAuthorityAndBlocksReplacementUntilActualCompletion(string mode, int later, string after)
    {
        using var f = new AudioLinuxFixture(); var r = await f.Run(mode);
        Assert.Equal("CleanupUncertain", r.GetProperty("Uncertain").GetString());
        Assert.InRange(r.GetProperty("ElapsedMs").GetDouble(), 1900, 3000);
        Assert.Equal(1, r.GetProperty("BlockedCount").GetInt32());
        Assert.True(r.GetProperty("OriginalDisposed").GetBoolean());
        Assert.Equal(later, r.GetProperty("LaterCount").GetInt32());
        Assert.Equal(after, r.GetProperty("After").GetString());
    }

    [Fact]
    public async Task PlaybackAndCleanupFailures_NoRetryAndNoReplacementWithCleanupDebt()
    {
        using var f = new AudioLinuxFixture(); var r = await f.Run("failure");
        Assert.Equal(1, r.GetProperty("NoRetryCount").GetInt32());
        Assert.Equal(2, r.GetProperty("RetainedCount").GetInt32());
        Assert.Equal("CleanupUncertain", r.GetProperty("Outcome").GetString());
        Assert.True(r.GetProperty("CleanupUnconfirmed").GetBoolean());
    }

    [Fact]
    public async Task ActualEngineRefreshAndExceptionExit_PreviewRestoresVolumeAndFinallyStopsAll()
    {
        using var f = new AudioLinuxFixture(); var r = await f.Run("preview-engine");
        Assert.True(r.GetProperty("PreviewStopped").GetBoolean());
        Assert.True(r.GetProperty("AcceptedSetting").GetBoolean());
        Assert.Equal(1, r.GetProperty("Volume").GetSingle());
        Assert.Equal(2, r.GetProperty("SameCount").GetInt32());
        Assert.Equal("NullReferenceException", r.GetProperty("EngineException").GetString());
        Assert.True(r.GetProperty("AllDisposed").GetBoolean());
    }

    [Fact]
    public async Task RealSdlQueueBackend_StreamsPcmAndChangesGainWithoutReopen()
    {
        using var f = new AudioLinuxFixture(); var r = await f.Run("sdl-volume");
        Assert.Equal("Stopped", r.GetProperty("Outcome").GetString());
        Assert.Equal(1, r.GetProperty("Opens").GetInt32());
        Assert.Equal(1, r.GetProperty("Closes").GetInt32());
        Assert.Equal(1, r.GetProperty("Quits").GetInt32());
        Assert.Equal(.25f, r.GetProperty("FirstSample").GetSingle());
        Assert.Equal(.5f, r.GetProperty("LaterSample").GetSingle());
        Assert.Equal(5, r.GetProperty("Queues").GetInt32());
    }

    [Theory]
    [InlineData("sdl-missing", "BackendUnavailable", 0, 0, 0)]
    [InlineData("sdl-no-device", "DeviceUnavailable", 1, 0, 1)]
    [InlineData("sdl-queue-error", "Error", 1, 1, 1)]
    [InlineData("sdl-close-error", "CleanupUncertain", 1, 1, 0)]
    public async Task RealSdlBackend_CapabilityAndPartialFailuresHaveExactCleanup(string mode, string outcome, int opens, int closes, int quits)
    {
        using var f = new AudioLinuxFixture(); var r = await f.Run(mode);
        Assert.Equal(outcome, r.GetProperty("Outcome").GetString());
        Assert.Equal(opens, r.GetProperty("Opens").GetInt32());
        Assert.Equal(closes, r.GetProperty("Closes").GetInt32());
        Assert.Equal(quits, r.GetProperty("Quits").GetInt32());
        Assert.Equal(r.GetProperty("OpensBefore").GetInt32(), r.GetProperty("Opens").GetInt32());
        Assert.Equal(0, r.GetProperty("DefaultDeviceCalls").GetInt32());
    }

    [Fact]
    public async Task ExplicitNativeDummyOnly_RealWavAndPositiveMp3DecodeQueueDrainAndClose()
    {
        using var f = new AudioLinuxFixture(); var r = await f.Run("dummy");
        Assert.True(r.GetProperty("DummyOnly").GetBoolean());
        Assert.True(r.GetProperty("DecodedSamples").GetInt32() > 0);
        Assert.Equal("Stopped", r.GetProperty("Outcome").GetString());
        Assert.Equal("Stopped", r.GetProperty("Final").GetString());
        Assert.Equal(r.GetProperty("Opens").GetInt32(), r.GetProperty("Closes").GetInt32());
        Assert.Equal(r.GetProperty("Initializes").GetInt32(), r.GetProperty("Quits").GetInt32());
        Assert.True(r.GetProperty("Opens").GetInt32() >= 2);
        Assert.Equal(0, r.GetProperty("DefaultDeviceCalls").GetInt32());
    }
}
