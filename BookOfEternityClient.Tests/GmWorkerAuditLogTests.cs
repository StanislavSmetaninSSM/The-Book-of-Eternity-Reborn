using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerAuditLogTests
{
    [Fact]
    public void SharedAuditEventIdGenerator_DeterministicInputsProduceReadableStableId()
    {
        var generatorType = typeof(GmWorkerAuditLog).Assembly.GetType(
            "BookOfEternityClient.Services.GmWorkers.GmWorkerAuditEventIdGenerator");
        Assert.NotNull(generatorType);
        var factory = generatorType.GetMethod(
            "Create",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(DateTimeOffset), typeof(Guid)],
            modifiers: null);
        Assert.NotNull(factory);

        var timestamp = new DateTimeOffset(2026, 7, 20, 3, 4, 5, 678, TimeSpan.Zero);
        var suffix = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

        var eventId = Assert.IsType<string>(factory.Invoke(null, [timestamp, suffix]));

        Assert.Equal("worker_audit_20260720030405678_00112233445566778899aabbccddeeff", eventId);
    }

    [Fact]
    public void SharedAuditEventIdGenerator_ConcurrentCallsProduceUniqueReadableIds()
    {
        var generatorType = typeof(GmWorkerAuditLog).Assembly.GetType(
            "BookOfEternityClient.Services.GmWorkers.GmWorkerAuditEventIdGenerator");
        Assert.NotNull(generatorType);
        var factory = generatorType.GetMethod(
            "Create",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        Assert.NotNull(factory);
        var eventIds = new string[10_000];

        Parallel.For(0, eventIds.Length, index =>
        {
            eventIds[index] = Assert.IsType<string>(factory.Invoke(null, null));
        });

        Assert.Equal(
            eventIds.Length,
            eventIds.Distinct(StringComparer.Ordinal).Count());
        Assert.All(eventIds, eventId =>
            Assert.Matches("^worker_audit_[0-9]{17}_[0-9a-f]{32}$", eventId));
    }

}
