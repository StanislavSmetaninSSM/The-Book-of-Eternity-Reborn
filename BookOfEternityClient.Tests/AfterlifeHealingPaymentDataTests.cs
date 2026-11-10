using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeHealingPaymentDataTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void PaymentDataPreservesTotalAbsenceAnnotationsAndEveryOtherDomain(bool total)
    {
        var quote = AfterlifeHealingServiceTestData.Quote(); var source = AfterlifeHealingServiceTestData.Soul(total: total);
        var original = source.ToJsonString();
        var payment = Assert.IsType<AfterlifeHealingServicePayment>(AfterlifeHealingServicePlanner.PreparePayment(quote, "attempt_fixture", source));
        var after = AfterlifeHealingServicePlanner.ComposeSoulAfterImage(source, quote, "attempt_fixture", payment);
        Assert.Equal(500 - quote.Amount, after["inkFeathers"]!["current"]!.GetValue<int>());
        Assert.Equal(total, after["inkFeathers"]!.AsObject().ContainsKey("total"));
        if (total) Assert.Equal(900, after["inkFeathers"]!["total"]!.GetValue<int>());
        Assert.Equal("retained wallet annotation", after["inkFeathers"]!["_note"]!.GetValue<string>());
        Assert.Equal("retained soul data", after["_untouched"]!.GetValue<string>());
        Assert.Equal(original, source.ToJsonString());
        Assert.Equal(payment, AfterlifeHealingServicePlanner.ParsePayment(payment.ToJson(), quote, "attempt_fixture"));
        payment.ToJson()["walletAfter"]!["current"] = 0;
        Assert.Equal(payment, AfterlifeHealingServicePlanner.ParsePayment(payment.ToJson(), quote, "attempt_fixture"));
        Assert.Throws<InvalidDataException>(() => AfterlifeHealingServicePlanner.ComposeSoulAfterImage(after, quote, "attempt_fixture", payment));
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(1)]
    public void MissingMalformedOrInsufficientWalletNeverClampsOrMutates(int current)
    {
        var quote = AfterlifeHealingServiceTestData.Quote(); var soul = AfterlifeHealingServiceTestData.Soul(current);
        var before = soul.ToJsonString();
        Assert.Null(AfterlifeHealingServicePlanner.PreparePayment(quote, "attempt_fixture", soul));
        Assert.Equal(before, soul.ToJsonString());
        soul.Remove("inkFeathers");
        Assert.Null(AfterlifeHealingServicePlanner.PreparePayment(quote, "attempt_fixture", soul));
    }

    [Theory]
    [InlineData("attemptId")] [InlineData("quoteFingerprint")] [InlineData("paymentFingerprint")] [InlineData("amount")]
    [InlineData("walletBefore")] [InlineData("walletAfter")] [InlineData("extra")]
    public void AlteredAttemptPriceOrWalletEvidenceRejects(string field)
    {
        var quote = AfterlifeHealingServiceTestData.Quote();
        var payment = Assert.IsType<AfterlifeHealingServicePayment>(AfterlifeHealingServicePlanner.PreparePayment(quote,
            "attempt_fixture", AfterlifeHealingServiceTestData.Soul()));
        var root = payment.ToJson();
        if (field.StartsWith("wallet", StringComparison.Ordinal)) root[field]!["current"] = 700;
        else root[field] = field == "amount" ? JsonValue.Create(0) : JsonValue.Create("different");
        Assert.Throws<InvalidDataException>(() => AfterlifeHealingServicePlanner.ParsePayment(root, quote, "attempt_fixture"));
    }
}
