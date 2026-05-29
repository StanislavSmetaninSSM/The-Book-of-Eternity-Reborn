using BookOfEternityClient.Configuration;
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
    private static readonly SemaphoreSlim _generateGate = new(1, 1);

    public BrowserMediaGenerationService(
        ImageService imageService,
        LocalMediaService mediaService,
        GameSettings settings)
    {
        _imageService = imageService;
        _mediaService = mediaService;
        _settings = settings;
    }

    public async Task<BrowserMediaGenerateResult> GenerateAsync(BrowserMediaGenerateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
            return new BrowserMediaGenerateResult(false, null, null, "Промпт не задан.");

        var provider = (_settings.ImageProvider ?? "placeholder").ToLowerInvariant();
        if (provider is "placeholder" or "none" or "off")
            return new BrowserMediaGenerateResult(false, null, null, "Генерация изображений отключена в настройках.");

        if (_imageService.EntityImageExists(request.EntityType, request.EntityKey))
        {
            var existingPath = _imageService.GetEntityImagePath(request.EntityType, request.EntityKey);
            if (existingPath != null)
            {
                var existingRef = _mediaService.TryCreateReference(existingPath);
                if (existingRef != null)
                    return new BrowserMediaGenerateResult(true, existingRef.MediaId, existingRef.Url, null);
            }
        }

        if (!await _generateGate.WaitAsync(TimeSpan.FromSeconds(1)))
            return new BrowserMediaGenerateResult(false, null, null, "Генерация уже выполняется. Дождитесь завершения.");

        try
        {
            var success = await _imageService.GenerateEntityImageAsync(
                request.Prompt, request.EntityType, request.EntityKey,
                displayAfterGenerate: false);

            if (!success)
                return new BrowserMediaGenerateResult(false, null, null, "Генерация не удалась. Проверьте подключение и API-ключ.");

            var path = _imageService.GetEntityImagePath(request.EntityType, request.EntityKey);
            if (path == null)
                return new BrowserMediaGenerateResult(false, null, null, "Файл не создан после генерации.");

            var reference = _mediaService.TryCreateReference(path);
            if (reference == null)
                return new BrowserMediaGenerateResult(false, null, null, "Файл создан, но не входит в разрешённые media-корни.");

            return new BrowserMediaGenerateResult(true, reference.MediaId, reference.Url, null);
        }
        finally
        {
            _generateGate.Release();
        }
    }
}
