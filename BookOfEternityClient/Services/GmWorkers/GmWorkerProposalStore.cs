using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

public sealed class GmWorkerProposalStore
{
    public const string ProposalRoot = "worker_proposals";

    private readonly FileSystemManager _fs;

    public GmWorkerProposalStore(FileSystemManager fs)
    {
        _fs = fs;
    }

    public async Task<string> SaveProposalAsync(WorkerProposal proposal)
    {
        if (!IsSafeId(proposal.ProposalId))
            throw new ArgumentException("Proposal id must be a safe lowercase identifier.", nameof(proposal));

        var path = GetProposalPath(proposal.ProposalId);
        await _fs.WriteFileAtomicAsync(path, GmWorkerJson.Serialize(proposal));
        return path;
    }

    public async Task<WorkerProposal?> ReadProposalAsync(string proposalId)
    {
        if (!IsSafeId(proposalId))
            throw new ArgumentException("Proposal id must be a safe lowercase identifier.", nameof(proposalId));

        var json = await _fs.ReadFileAsync(GetProposalPath(proposalId));
        return string.IsNullOrWhiteSpace(json)
            ? null
            : GmWorkerJson.Deserialize<WorkerProposal>(json);
    }

    public static string GetProposalPath(string proposalId) =>
        $"{ProposalRoot}/{proposalId}/proposal.json";

    private static bool IsSafeId(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(ch => char.IsLower(ch) || char.IsDigit(ch) || ch is '_' or '-');
}
