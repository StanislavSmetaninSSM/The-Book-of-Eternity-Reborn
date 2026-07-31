using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.WebUi;

public sealed record BrowserMediaGenerateRequest(
    string Prompt,
    string EntityType,
    string EntityKey);

public sealed record BrowserMediaGenerateResult(
    bool Success,
    string? MediaId,
    string? Url,
    string? ErrorMessage);

public sealed class BrowserMediaGenerationService
{
    private readonly ImageService _imageService;
    private readonly LocalMediaService _mediaService;
    private readonly GameSettings _settings;
    private readonly FileSystemManager _fs;
    private readonly Func<BrowserMediaGenerateRequest, Task<StagedEntityImage?>> _stageImageAsync;
    private static readonly SemaphoreSlim _generateGate = new(1, 1);

    public BrowserMediaGenerationService(
        ImageService imageService,
        LocalMediaService mediaService,
        GameSettings settings,
        FileSystemManager fs)
        : this(
            imageService,
            mediaService,
            settings,
            fs,
            request => imageService.StageEntityImageAsync(
                request.Prompt,
                request.EntityType,
                request.EntityKey))
    {
    }

    internal BrowserMediaGenerationService(
        ImageService imageService,
        LocalMediaService mediaService,
        GameSettings settings,
        FileSystemManager fs,
        Func<BrowserMediaGenerateRequest, Task<StagedEntityImage?>> stageImageAsync)
    {
        _imageService = imageService;
        _mediaService = mediaService;
        _settings = settings;
        _fs = fs;
        _stageImageAsync = stageImageAsync;
    }

    public async Task<BrowserMediaGenerateResult> GenerateAsync(BrowserMediaGenerateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
            return new BrowserMediaGenerateResult(false, null, null, "Промпт не задан.");
        if (!ImageService.IsSupportedEntityType(request.EntityType))
            return new BrowserMediaGenerateResult(false, null, null, "Этот тип сущности не поддерживает изображения.");

        var provider = (_settings.ImageProvider ?? "placeholder").ToLowerInvariant();
        if (provider is "placeholder" or "none" or "off")
            return new BrowserMediaGenerateResult(false, null, null, "Генерация изображений отключена в настройках.");

        string generation;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
            generation = _fs.GetOrCreateSessionGeneration(writeLease);

        try
        {
            return await SessionOperationContext.RunBoundAsync(
                _fs,
                generation,
                () => GenerateBoundAsync(request));
        }
        catch (SessionReplacedException)
        {
            return new BrowserMediaGenerateResult(
                false,
                null,
                null,
                "Игровая сессия изменилась во время генерации. Изображение старой сессии не сохранено.");
        }
        catch (Exception ex) when (ex is InvalidDataException or UnauthorizedAccessException)
        {
            return new BrowserMediaGenerateResult(
                false,
                null,
                null,
                "Не удалось безопасно сохранить изображение в каталоге текущей игры.");
        }
    }

    private async Task<BrowserMediaGenerateResult> GenerateBoundAsync(
        BrowserMediaGenerateRequest request)
    {
        var existingRef = await TryGetExistingReferenceAsync(request);
        if (existingRef != null)
            return new BrowserMediaGenerateResult(true, existingRef.MediaId, existingRef.Url, null);

        if (!await _generateGate.WaitAsync(TimeSpan.FromSeconds(1)))
            return new BrowserMediaGenerateResult(false, null, null, "Генерация уже выполняется. Дождитесь завершения.");

        try
        {
            var staged = await _stageImageAsync(request);
            if (staged == null)
            {
                return new BrowserMediaGenerateResult(
                    false,
                    null,
                    null,
                    "Генерация не удалась. Проверьте подключение и API-ключ.");
            }

            var reference = await CommitStagedImageAsync(staged);
            if (reference == null)
                return new BrowserMediaGenerateResult(false, null, null, "Файл создан, но не входит в разрешённые media-корни.");

            return new BrowserMediaGenerateResult(true, reference.MediaId, reference.Url, null);
        }
        finally
        {
            _generateGate.Release();
        }
    }

    private async Task<LocalMediaReference?> TryGetExistingReferenceAsync(
        BrowserMediaGenerateRequest request)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        if (!_imageService.EntityImageExists(request.EntityType, request.EntityKey))
            return null;

        var existingPath = _imageService.GetEntityImagePath(request.EntityType, request.EntityKey);
        return existingPath == null ? null : _mediaService.TryCreateReference(existingPath);
    }

    private async Task<LocalMediaReference?> CommitStagedImageAsync(StagedEntityImage staged)
    {
        var normalizedPath = staged.CanonicalRelativePath.Replace('\\', '/').Trim();
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(normalizedPath) ||
            !normalizedPath.StartsWith("images/", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetExtension(normalizedPath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        await _fs.WriteFileAtomicBytesAsync(
            writeLease,
            normalizedPath,
            staged.Content);
        return _mediaService.TryCreateReference(_fs.ResolvePath(normalizedPath));
    }
}
