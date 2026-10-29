using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.Services;

internal sealed record ElyaraVisitSourceImage(string Path, CanonicalBeforeImage Image);
internal sealed record ElyaraVisitSourceRead(ElyaraVisitPlan Plan, IReadOnlyList<ElyaraVisitSourceImage> Images);

internal static partial class ElyaraVisitService
{
    internal static IReadOnlyList<string> SourcePaths { get; } = Array.AsReadOnly(new[]
    {
        "game_state/meta/soul_state.json", AfterlifeEntityProfileState.StatePath,
        "game_state/meta/guardians.json", ShiningAbodeState.StatePath, AfterlifeSpiritualConflictState.StatePath
    });

    internal static async Task<ExplorerCommandResult> OpenAsync(FileSystemManager files)
    {
        ElyaraVisitPlan plan;
        try
        {
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            plan = (await ReadAsync(files, lease)).Plan;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException || IsInputFailure(error))
        { plan = Unavailable(); }
        // Opening treatment obtains its own new source context and confirmation.
        // No visit permission or old quote crosses this navigation boundary.
        return plan.Kind == ElyaraVisitKind.CoLocated
            ? await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true)
            : ExplorerElyaraCommandResultBuilder.Build(plan);
    }

    internal static async Task<ElyaraVisitSourceRead> ReadAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease)
    {
        files.VerifyCurrentSessionOperation(lease);
        var generation = files.GetOrCreateSessionGeneration(lease);
        var roots = new JsonObject?[SourcePaths.Count];
        var images = new List<ElyaraVisitSourceImage>(SourcePaths.Count);
        for (var index = 0; index < SourcePaths.Count; index++)
        {
            var path = SourcePaths[index];
            var bytes = await files.ReadFileBytesAsync(lease, path);
            images.Add(new(path, new CanonicalBeforeImage(bytes is not null, bytes)));
            if (bytes is not null)
                roots[index] = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes),
                    new JsonSerializerOptions(), path) ?? throw new InvalidDataException("Expected current visit source object.");
        }
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[] { "elyara_visit_source", "1", generation }
            .Concat(images.SelectMany(image => new[] { image.Path, image.Image.Fingerprint })));
        var input = new ElyaraVisitInput(roots[0], roots[1], roots[2], roots[3], roots[4]);
        return new(Project(input, generation, fingerprint), images.AsReadOnly());
    }

    internal static async Task<string> ConfirmAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        ElyaraVisitCommandState state)
    {
        var current = await RequireCurrentAsync(files, lease, state.Intent);
        _ = current; // The original source image owner is needed only through this final admission.
        return ActionFor(state.Intent);
    }

    internal static async Task<ElyaraVisitSourceRead> RequireCurrentAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, ElyaraVisitIntent intent)
    {
        files.VerifyCurrentSessionOperation(lease);
        if (!files.IsCurrentSessionGeneration(lease, intent.Generation))
            throw new InvalidDataException("Сессия изменилась; откройте путь к Элиаре заново.");
        if (AfterlifeLocalActionGuard.TryDescribeActiveGmTurnLifecycleBlocker(files, lease,
            "Переход к Элиаре", "текущую Обитель и один цикл мира") is not null)
            throw new InvalidDataException("Сначала завершите текущий ход; путь к Элиаре не отправлен.");
        var current = await ReadAsync(files, lease);
        if (current.Plan.Kind != ElyaraVisitKind.ChaosTravel || current.Plan.State?.Intent != intent)
            throw new InvalidDataException("Местонахождение Элиары или доступный путь изменились; откройте предложение заново.");
        return current;
    }

    internal static async Task RequireImagesCurrentAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, ElyaraVisitSourceRead source)
    {
        files.VerifyCurrentSessionOperation(lease);
        foreach (var original in source.Images)
        {
            var bytes = await files.ReadFileBytesAsync(lease, original.Path);
            if (new CanonicalBeforeImage(bytes is not null, bytes).Fingerprint != original.Image.Fingerprint)
                throw new InvalidDataException("Current Elyara visit source changed before staging: " + original.Path);
        }
    }
}
