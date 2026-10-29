using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Detached exact wallet evidence; not proof that a payment was published.</summary>
internal sealed record AfterlifeHealingServicePayment(string AttemptId, string QuoteFingerprint,
    WoundOwnerCoordinate Payer, int Amount, string WalletBeforeJson, string WalletAfterJson, string Fingerprint)
{
    internal JsonObject ToJson() => AfterlifeHealingServicePlanner.PaymentJson(this);
}

/// <summary>Pure contribution to the existing common owner-companion plan; never a second writer.</summary>
internal static class AfterlifeHealingServicePlanner
{
    internal static AfterlifeHealingServicePayment? PreparePayment(AfterlifeHealingServiceQuote quote,
        string attemptId, JsonObject soul)
    {
        quote = AfterlifeHealingServiceContract.ParseQuote(quote.ToJson());
        if (!ResourceMaterializationContract.IsExactIdentifier(attemptId) ||
            !GuardianPolicyContracts.TryPrepareHealingFeatherWallet(soul, quote.Amount, out var after)) return null;
        var payment = new AfterlifeHealingServicePayment(attemptId, quote.Fingerprint, quote.Patient, quote.Amount,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(soul["inkFeathers"]!),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(after!), "");
        return payment with { Fingerprint = PaymentFingerprint(payment) };
    }

    internal static JsonObject ComposeSoulAfterImage(JsonObject soul, AfterlifeHealingServiceQuote quote,
        string attemptId, AfterlifeHealingServicePayment payment)
    {
        var exact = PreparePayment(quote, attemptId, soul);
        if (exact is null || exact != payment)
            throw new InvalidDataException("Service payment differs from its exact original wallet or quote.");
        var after = soul.DeepClone().AsObject();
        after["inkFeathers"] = JsonNode.Parse(payment.WalletAfterJson);
        return after;
    }

    internal static AfterlifeHealingServicePayment ParsePayment(JsonNode? node,
        AfterlifeHealingServiceQuote quote, string attemptId)
    {
        if (node is not JsonObject root || !AfterlifeHealingServiceContract.Closed(root, "schemaVersion", "kind", "attemptId",
                "quoteFingerprint", "payer", "amount", "walletBefore", "walletAfter", "paymentFingerprint") ||
            AfterlifeHealingServiceContract.Number(root["schemaVersion"]) != 1 ||
            AfterlifeHealingServiceContract.Text(root["kind"]) != "healing_service_payment" ||
            root["walletBefore"] is not JsonObject before || root["walletAfter"] is not JsonObject after)
            throw new InvalidDataException("Invalid closed healing payment evidence.");
        var soul = new JsonObject { ["inkFeathers"] = before.DeepClone() };
        var expected = PreparePayment(quote, attemptId, soul);
        var actual = new AfterlifeHealingServicePayment(AfterlifeHealingServiceContract.Text(root["attemptId"]) ?? "",
            AfterlifeHealingServiceContract.Text(root["quoteFingerprint"]) ?? "", SpiritualTreatmentParticipation.ReadOwner(root["payer"]),
            AfterlifeHealingServiceContract.Number(root["amount"]) ?? 0, WoundAcceptedTurnFingerprintWriter.CanonicalJson(before),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(after), AfterlifeHealingServiceContract.Text(root["paymentFingerprint"]) ?? "");
        if (expected is null || actual != expected)
            throw new InvalidDataException("Payment wallet, attempt, price or fingerprint differs.");
        return actual;
    }

    internal static JsonObject PaymentJson(AfterlifeHealingServicePayment payment) => new()
    {
        ["schemaVersion"] = 1, ["kind"] = "healing_service_payment", ["attemptId"] = payment.AttemptId,
        ["quoteFingerprint"] = payment.QuoteFingerprint, ["payer"] = SpiritualHealingRequestProjection.OwnerJson(payment.Payer),
        ["amount"] = payment.Amount, ["walletBefore"] = JsonNode.Parse(payment.WalletBeforeJson),
        ["walletAfter"] = JsonNode.Parse(payment.WalletAfterJson), ["paymentFingerprint"] = payment.Fingerprint
    };

    private static string PaymentFingerprint(AfterlifeHealingServicePayment payment)
    {
        var root = PaymentJson(payment); root.Remove("paymentFingerprint");
        return AfterlifeHealingServiceContract.Fingerprint(root, "payment");
    }
}
