using System.Globalization;

namespace BookOfEternityClient.Services.GmWorkers;

internal static class GmWorkerAuditEventIdGenerator
{
    internal static string Create() => Create(DateTimeOffset.UtcNow, Guid.NewGuid());

    internal static string Create(DateTimeOffset timestamp, Guid uniqueSuffix) =>
        "worker_audit_" +
        timestamp.ToUniversalTime().ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) +
        "_" +
        uniqueSuffix.ToString("N");
}
