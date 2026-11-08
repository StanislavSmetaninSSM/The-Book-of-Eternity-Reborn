using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal IReadOnlyList<ValidationIssue> ValidateNaturalConflictStartFrame(JsonObject raw,
        SpiritualConflictValidationFrame projected)
    {
        var issues = new List<ValidationIssue>();
        ValidateAfterlifeSpiritualConflictUpdateContract(JsonSerializer.SerializeToElement(raw),
            AfterlifeSpiritualConflictState.StatePath, issues);
        issues.AddRange(EvaluateSpiritualConflictValidationFrame(projected));
        return issues.AsReadOnly();
    }
}
