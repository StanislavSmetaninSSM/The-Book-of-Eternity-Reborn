using System.Text.Json;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>Renders private QTE failure DATA only for explicit local tests, without installing filesystem observers.</summary>
internal static class JointQteDiagnosticCapture
{
    /// <summary>Runs the actual supplied operation and preserves the same failure even if diagnostic rendering fails.</summary>
    /// <typeparam name="T">Unchanged operation result.</typeparam>
    /// <param name="operation">Actual test invocation with its original source and lease scopes.</param>
    /// <param name="write">Local diagnostic output; never a game/GM-facing logger.</param>
    /// <returns>The unchanged successful result.</returns>
    internal static async Task<T> RunAsync<T>(Func<Task<T>> operation, Action<string> write)
    {
        try { return await operation(); }
        catch (Exception failure)
        {
            try { Write(failure, write); }
            catch (Exception diagnosticFailure)
            {
                try { write("Joint QTE diagnostic capture failed: " + diagnosticFailure); }
                catch { /* A failed diagnostic sink cannot replace the real operation failure. */ }
            }
            throw;
        }
    }

    /// <summary>Visits the existing exception graph once and renders only explicitly attached QTE diagnostics.</summary>
    /// <param name="failure">Actual propagated operation failure.</param>
    /// <param name="write">Local test output receiver.</param>
    private static void Write(Exception failure, Action<string> write)
    {
        var seen = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        void Visit(Exception current)
        {
            if (!seen.Add(current)) return;
            if (MortalJointRepairReadDiagnostic.From(current) is { } diagnostic)
            {
                write(JsonSerializer.Serialize(new
                {
                    Kind = "JointQteReadDiagnostic",
                    diagnostic.Origin,
                    Issues = diagnostic.Issues.Select(Issue),
                    SourceIssues = diagnostic.SourceIssues.Select(Issue),
                    CaughtException = diagnostic.CaughtException?.ToString(),
                    CaptureFailure = diagnostic.CaptureFailure?.ToString()
                }));
            }
            if (current is AggregateException aggregate)
                foreach (var child in aggregate.InnerExceptions) Visit(child);
            else if (current.InnerException is { } inner) Visit(inner);
        }
        Visit(failure);
    }

    /// <summary>Projects all existing issue fields into local JSON without serializing Exception objects.</summary>
    /// <param name="issue">Detached original issue.</param>
    /// <returns>Explicit diagnostic fields including private repair DATA.</returns>
    private static object Issue(ValidationIssue issue) => new
    {
        issue.FilePath, issue.Severity, issue.Message, issue.Category, issue.Code,
        issue.Actor, issue.Section, issue.Expected, issue.Actual, issue.RepairHint,
        issue.RepairTargetFiles, issue.FactionRepairClassification, issue.MortalItemRepairContext,
        issue.MortalLocationRepairContext, issue.EffectRepairContext,
        WoundRepairContext = issue.WoundRepairContext is { } wound ? new
        {
            wound.SessionId, wound.RequestId, wound.SnapshotToken, wound.Kind, wound.CandidateRef,
            wound.SemanticFingerprint, wound.OpportunityRef, wound.SafeContext, wound.AllowedDecisions,
            wound.MinimumSeverity, wound.MaximumSeverity, wound.RejectedDecision, wound.OpportunityAuthorityFingerprint
        } : null
    };
}
