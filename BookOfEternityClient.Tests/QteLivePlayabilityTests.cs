using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class QteLivePlayabilityTests
{
    [Fact]
    public async Task QteLive_TimingBarLoopRecordsDifficultyBoundedFramesThroughLiveRenderer()
    {
        var easy = QteSceneService.ComputeTimingBarLiveRequirement(1, statTier: 0);
        var hard = QteSceneService.ComputeTimingBarLiveRequirement(5, statTier: 0);
        var clock = new FakeLiveClock();
        var input = new ScheduledConsoleInputSource(clock, [
            new ScheduledKey(hard.SuccessStart * hard.TickMs, Key(ConsoleKey.Spacebar))
        ]);
        var renderer = new RecordingLiveRenderer(
            "Полоса реакции",
            "Нажмите Space, когда маркер будет в центральной зоне.",
            "");

        var grade = await QteSceneService.RunTimingBarLiveLoopAsync(hard, input, renderer, clock);

        Assert.Equal("success", grade);
        Assert.True(hard.TickMs < easy.TickMs);
        Assert.True(hard.SuccessWindowMs < easy.SuccessWindowMs);
        Assert.True(hard.TimeoutMs < easy.TimeoutMs);
        Assert.InRange(hard.SuccessWindowMs, 80, 220);
        Assert.InRange(hard.TimeoutMs, 1200, 3000);
        Assert.Contains(hard.TickMs, clock.Delays);
        Assert.Contains(renderer.Frames, frame =>
            frame.Body.Contains("Окно успеха:", StringComparison.Ordinal) &&
            frame.Body.Contains("Осталось:", StringComparison.Ordinal));
        Assert.True(renderer.Frames.Count > 2);
    }

    [Fact]
    public void QteLive_TimingBarHardDifficultyStaysTightWithHighStat()
    {
        var normalBoosted = QteSceneService.ComputeTimingBarLiveRequirement(3, statTier: 3);
        var hardBoosted = QteSceneService.ComputeTimingBarLiveRequirement(5, statTier: 3);

        Assert.True(hardBoosted.TickMs < normalBoosted.TickMs);
        Assert.True(hardBoosted.SuccessWidth <= 4);
        Assert.True(hardBoosted.SuccessWindowMs <= 240);
        Assert.True(hardBoosted.TimeoutMs < normalBoosted.TimeoutMs);
    }

    [Fact]
    public async Task QteLive_PromptChainFirstPromptSurvivesPastBaseTimeoutThroughLiveLoop()
    {
        var requirement = QteSceneService.ComputePromptChainLiveRequirement(5, statTier: 0);
        var inputAtMs = requirement.PerPromptTimeoutMs + 100;
        var clock = new FakeLiveClock();
        var input = new ScheduledConsoleInputSource(clock, [
            new ScheduledKey(inputAtMs, Key(ConsoleKey.W))
        ]);
        var renderer = new RecordingLiveRenderer(
            "Цепь знаков",
            "Нажимайте показанную физическую клавишу до истечения таймера.",
            "");

        var grade = await QteSceneService.RunPromptChainLiveLoopAsync(
            requirement,
            [ConsoleKey.W],
            input,
            renderer,
            clock);

        Assert.Equal("success", grade);
        Assert.True(inputAtMs < requirement.FirstPromptTimeoutMs);
        Assert.True(clock.ElapsedMs >= inputAtMs);
        Assert.Contains(renderer.Frames, frame =>
            frame.Body.Contains("Текущий знак:", StringComparison.Ordinal) &&
            frame.Body.Contains("Шаг 1/1", StringComparison.Ordinal) &&
            frame.Body.Contains("Осталось:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task QteLive_BalanceMeterLiveLoopShowsControlCopyAndAppliesSameStep()
    {
        var requirement = QteSceneService.ComputeBalanceMeterLiveRequirement(baseDifficulty: 3, statTier: 0);
        var clock = new FakeLiveClock();
        var input = new ScheduledConsoleInputSource(clock, [
            new ScheduledKey(0, Key(ConsoleKey.D))
        ]);
        var renderer = new RecordingLiveRenderer(
            "Равновесие",
            "Удерживайте индикатор в центральной зоне.",
            "");

        var grade = await QteSceneService.RunBalanceMeterLiveLoopAsync(
            requirement,
            input,
            renderer,
            clock,
            nextDrift: (_, _) => 0);

        Assert.Equal("success", grade);
        Assert.Contains(requirement.TickMs, clock.Delays);
        Assert.Contains(renderer.Frames, frame =>
            frame.Body.Contains("Позиция: 60/100", StringComparison.Ordinal) &&
            frame.Body.Contains("безопасная зона:", StringComparison.OrdinalIgnoreCase) &&
            frame.Body.Contains("D/В или →: вправо на 10", StringComparison.Ordinal) &&
            frame.Body.Contains("A/Ф или ←: влево на 10", StringComparison.Ordinal) &&
            frame.Body.Contains("Шаг управления: 10", StringComparison.Ordinal));
    }

    [Fact]
    public async Task QteLive_BalanceMeterFrameSeparatesPlayerInputFromDrift()
    {
        var requirement = QteSceneService.ComputeBalanceMeterLiveRequirement(baseDifficulty: 3, statTier: 0);
        var clock = new FakeLiveClock();
        var input = new ScheduledConsoleInputSource(clock, [
            new ScheduledKey(0, Key(ConsoleKey.D))
        ]);
        var renderer = new RecordingLiveRenderer(
            "Равновесие",
            "Удерживайте индикатор в центральной зоне.",
            "");

        _ = await QteSceneService.RunBalanceMeterLiveLoopAsync(
            requirement,
            input,
            renderer,
            clock,
            nextDrift: (_, _) => -7);

        Assert.Contains(renderer.Frames, frame =>
            frame.Body.Contains("Позиция: 53/100", StringComparison.Ordinal) &&
            frame.Body.Contains("Игрок: вправо +10", StringComparison.Ordinal) &&
            frame.Body.Contains("Помеха: -7", StringComparison.Ordinal) &&
            frame.Body.Contains("Итог: +3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task QteLive_PatternMemoryLiveLoopReplacesRevealFrameBeforeInput()
    {
        var sequence = new[] { "q", "w", "space" };
        var effective = new QteSceneService.PatternMemoryEffectiveRequirement(
            SequenceLength: 3,
            RevealMs: 60,
            InputTimeoutMs: 1000,
            AllowedMistakes: 0);
        var clock = new FakeLiveClock();
        var input = new ScheduledConsoleInputSource(clock, [
            new ScheduledKey(60, Key(ConsoleKey.Q)),
            new ScheduledKey(60, Key(ConsoleKey.W)),
            new ScheduledKey(60, Key(ConsoleKey.Spacebar))
        ]);
        var renderer = new RecordingLiveRenderer(
            "Память рун: фаза показа",
            "Запомните порядок знаков. Ввод начнётся после показа.",
            QteSceneService.BuildPatternMemoryRevealFrame(sequence, effective.RevealMs));

        var grade = await QteSceneService.RunPatternMemoryLiveLoopAsync(
            sequence,
            effective,
            input,
            renderer,
            clock);

        Assert.Equal("success", grade);
        Assert.Contains(renderer.Frames, frame =>
            frame.Title == "Память рун: фаза показа" &&
            frame.Body.Contains("Показ:", StringComparison.Ordinal) &&
            frame.Body.Contains("Q / Й", StringComparison.Ordinal) &&
            frame.Body.Contains("W / Ц", StringComparison.Ordinal) &&
            frame.Body.Contains("Space", StringComparison.Ordinal));

        var inputFrames = renderer.Frames
            .Where(frame => frame.Title == "Память рун: фаза ввода")
            .ToArray();
        Assert.NotEmpty(inputFrames);
        Assert.All(inputFrames, frame =>
        {
            Assert.Contains("Введено:", frame.Body, StringComparison.Ordinal);
            Assert.Contains("Шаг", frame.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("Показ:", frame.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("Q / Й  W / Ц  Space", frame.Body, StringComparison.Ordinal);
        });
    }

    private sealed class RecordingLiveRenderer : QteSceneService.IQteMiniGameLiveRenderer
    {
        private string _title;
        private string _instructions;

        public RecordingLiveRenderer(string title, string instructions, string body)
        {
            _title = title;
            _instructions = instructions;
            Frames.Add(new LiveFrame(title, instructions, body));
        }

        public List<LiveFrame> Frames { get; } = [];

        public void Update(string body)
        {
            Update(_title, _instructions, body);
        }

        public void Update(string title, string instructions, string body)
        {
            _title = title;
            _instructions = instructions;
            Frames.Add(new LiveFrame(title, instructions, body));
        }
    }

    private sealed class FakeLiveClock : QteSceneService.IQteLiveClock
    {
        private readonly DateTime _start = new(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc);
        private DateTime _now = new(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc);

        public DateTime UtcNow => _now;

        public int ElapsedMs => (int)(_now - _start).TotalMilliseconds;

        public List<int> Delays { get; } = [];

        public Task DelayAsync(int milliseconds)
        {
            Delays.Add(milliseconds);
            _now = _now.AddMilliseconds(milliseconds);
            return Task.CompletedTask;
        }
    }

    private sealed class ScheduledConsoleInputSource : IConsoleInputSource
    {
        private readonly FakeLiveClock _clock;
        private readonly Queue<ScheduledKey> _keys;

        public ScheduledConsoleInputSource(FakeLiveClock clock, IEnumerable<ScheduledKey> keys)
        {
            _clock = clock;
            _keys = new Queue<ScheduledKey>(keys.OrderBy(key => key.AtMs));
        }

        public bool IsScripted => true;

        public bool KeyAvailable => _keys.Count > 0 && _clock.ElapsedMs >= _keys.Peek().AtMs;

        public ConsoleKeyInfo ReadKey(bool intercept = true) =>
            KeyAvailable ? _keys.Dequeue().KeyInfo : Key(ConsoleKey.Enter);

        public string? ReadLine() => string.Empty;

        public void AssertCompleted()
        {
            Assert.Empty(_keys);
        }
    }

    private sealed record LiveFrame(string Title, string Instructions, string Body);

    private readonly record struct ScheduledKey(int AtMs, ConsoleKeyInfo KeyInfo);

    private static ConsoleKeyInfo Key(ConsoleKey key)
    {
        var keyChar = key == ConsoleKey.Spacebar ? ' ' : char.ToLowerInvariant(key.ToString()[0]);
        return new ConsoleKeyInfo(keyChar, key, false, false, false);
    }
}
