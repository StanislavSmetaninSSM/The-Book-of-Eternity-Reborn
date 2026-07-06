using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AcceptedTurnNarrativePayloadValidationTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public AcceptedTurnNarrativePayloadValidationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "boe-narrative-validation-" + Guid.NewGuid().ToString("N"));
        _fs = new FileSystemManager(_tempRoot, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public async Task ValidateAcceptedTurnNarrativePayloadAsync_RejectsTechnicalRepairLeakInPlayerNarrative()
    {
        await _fs.WriteFileAtomicAsync("output/narrative_response.json", """
        {
          "response": "Оплаченный урок у Мирона завершён и теперь записан в состояние навыков корректно: \"Ножевой бой\" открыт как активный навык. Записи навыка и мастерства сохранены как массивы, поэтому будущие тренировки смогут добавляться рядом, не ломая витрину развития.",
          "timestamp": "2026-07-06T15:52:41Z"
        }
        """);

        var issues = await _validator.ValidateAcceptedTurnNarrativePayloadAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "narrative_response_technical_repair_leak", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "output/narrative_response.json.response", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnNarrativePayloadAsync_AllowsOrdinaryFantasyUseOfSimilarWords()
    {
        await _fs.WriteFileAtomicAsync("output/narrative_response.json", """
        {
          "response": "Мирон провёл Лиру к массивной дубовой двери, за которой пахло мокрой щепой и старым железом. Он показал короткий охотничий выпад без лишних слов.",
          "timestamp": "2026-07-06T15:52:41Z"
        }
        """);

        var issues = await _validator.ValidateAcceptedTurnNarrativePayloadAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "narrative_response_technical_repair_leak", StringComparison.OrdinalIgnoreCase));
    }
}
