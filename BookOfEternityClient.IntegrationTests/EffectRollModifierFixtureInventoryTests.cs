using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "RegressionIntegration")]
public sealed class EffectRollModifierFixtureInventoryTests
{
    private sealed record InventoryOwner(
        string RelativePath,
        string TypeName,
        string MemberName);

    private static readonly IReadOnlyDictionary<InventoryOwner, int>
        AllowedContributionWriters = new Dictionary<InventoryOwner, int>
        {
            [new(
                "BookOfEternityClient.TestSupport/EffectMaterializationTestFixture.cs",
                "EffectMaterializationTestFixture",
                "CreateRollModifierPayload")] = 1,
            [new(
                "BookOfEternityClient.Tests/EffectRollContributionResolverTests.cs",
                "EffectRollContributionResolverTests",
                "Authority_OperationAndJsonRowBoundsFailBeforeDetachedCopies")] = 1
        };

    private static readonly IReadOnlyDictionary<InventoryOwner, int>
        AllowedMissingScopeMutations = new Dictionary<InventoryOwner, int>
        {
            [new(
                "BookOfEternityClient.Tests/EffectMaterializationContractTests.cs",
                "EffectMaterializationContractTests",
                "Validate_RollModifier_RequiresClosedStructuralScopeUnion")] = 1,
            [new(
                "BookOfEternityClient.Tests/EffectSourceDefinitionContractTests.cs",
                "EffectSourceDefinitionContractTests",
                "ValidateArray_RollModifier_RequiresClosedStructuralScopeUnion")] = 1
        };

    private static readonly string[] FileBackedRollSnapshotFactNames =
    [
        "Resolve_LoadAsyncMalformedSkillRoot_FailsClosedWithSnapshotDiagnostics",
        "Resolve_LoadAsyncWithExistingPublicationLease_UsesExactCurrentSkillAuthority"
    ];

    [Fact]
    public void ExecutableCSharpRollPayloadsUseCentralClosedFactoryAndOnlyNamedNegativeScopeMutations()
    {
        var contributionCounts = AllowedContributionWriters.Keys.ToDictionary(
            static owner => owner,
            static _ => 0);
        var removalCounts = AllowedMissingScopeMutations.Keys.ToDictionary(
            static owner => owner,
            static _ => 0);
        var violations = new List<string>();

        foreach (var (relativePath, source) in EnumerateExecutableCSharpSources())
        {
            InspectSource(
                relativePath,
                source,
                AllowedContributionWriters,
                contributionCounts,
                AllowedMissingScopeMutations,
                removalCounts,
                violations);
        }

        AssertExactInventory(
            "contribution writer",
            AllowedContributionWriters,
            contributionCounts,
            violations);
        AssertExactInventory(
            "missing-scope mutation",
            AllowedMissingScopeMutations,
            removalCounts,
            violations);
        Assert.True(
            violations.Count == 0,
            "The syntax-aware executable roll-modifier fixture inventory changed:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void SyntaxInventoryDetectsEquivalentCSharpAndJsonBypasses()
    {
        AssertViolationDetected(
            """
            sealed class Probe
            {
                void Run(dynamic component) =>
                    component["payload"]!.AsObject().Remove("scope");
            }
            """,
            "unlisted Remove(\"scope\") mutation");
        AssertViolationDetected(
            """
            sealed class Probe
            {
                void Run(dynamic component, dynamic value) =>
                    component["payload"]!.AsObject().Add("contribution", value);
            }
            """,
            "direct contribution Add/TryAdd writer");
        AssertViolationDetected(
            """
            sealed class Probe
            {
                void Run(dynamic component, dynamic value) =>
                    component["payload"]!["contribution"] = value;
            }
            """,
            "direct contribution writer");
        AssertViolationDetected(
            """
            sealed class Probe
            {
                string Build(string direction) =>
                    JsonNode.Parse($"{{\"operations\":[\"skill_check\"],\"contribution\":\"{direction}\"}}")!
                        .ToJsonString();
            }
            """,
            "interpolated JSON roll payload bypasses the semantic fixture factory");
        AssertViolationDetected(
            """
            sealed class Probe
            {
                object? Build() => JsonNode.Parse(
                    "{\"operations\":[\"attack_roll\"]," +
                    "\"contribution\":\"advantage\",\"scope\":{\"kind\":\"all\"}}");
            }
            """,
            "direct JSON-string roll payload bypasses the semantic fixture factory");
    }

    [Fact]
    public void FileBackedRollSnapshotFactsAreIntegrationOwnedAndFastResolverStaysPure()
    {
        var executableSources = EnumerateExecutableCSharpSources().ToArray();
        var ownedSources = executableSources
            .Where(static entry => entry.RelativePath is
                "BookOfEternityClient.IntegrationTests/EffectRollContributionSnapshotTests.cs" or
                "BookOfEternityClient.Tests/EffectRollContributionResolverTests.cs")
            .ToDictionary(
                static entry => entry.RelativePath,
                static entry => CSharpSyntaxTree.ParseText(
                        entry.Source,
                        path: entry.RelativePath)
                    .GetCompilationUnitRoot(),
                StringComparer.Ordinal);

        var integrationRoot = ownedSources[
            "BookOfEternityClient.IntegrationTests/EffectRollContributionSnapshotTests.cs"];
        var integrationClass = Assert.Single(
            integrationRoot.DescendantNodes().OfType<ClassDeclarationSyntax>(),
            static declaration =>
                declaration.Identifier.ValueText == "EffectRollContributionSnapshotTests");
        var fastRoot = ownedSources[
            "BookOfEternityClient.Tests/EffectRollContributionResolverTests.cs"];
        var fastClass = Assert.Single(
            fastRoot.DescendantNodes().OfType<ClassDeclarationSyntax>(),
            static declaration =>
                declaration.Identifier.ValueText == "EffectRollContributionResolverTests");

        foreach (var factName in FileBackedRollSnapshotFactNames)
        {
            var ownedIntegrationFact = Assert.Single(
                integrationClass.Members.OfType<MethodDeclarationSyntax>(),
                method => method.Identifier.ValueText == factName);
            var integrationMethods = executableSources
                .Where(static entry => entry.RelativePath.StartsWith(
                    "BookOfEternityClient.IntegrationTests/",
                    StringComparison.Ordinal))
                .Where(entry => entry.Source.Contains(factName, StringComparison.Ordinal))
                .SelectMany(static entry => CSharpSyntaxTree.ParseText(
                        entry.Source,
                        path: entry.RelativePath)
                    .GetCompilationUnitRoot()
                    .DescendantNodes()
                    .OfType<MethodDeclarationSyntax>());
            var integrationFact = Assert.Single(
                integrationMethods,
                method => method.Identifier.ValueText == factName);
            Assert.Equal(
                ownedIntegrationFact.SyntaxTree.FilePath,
                integrationFact.SyntaxTree.FilePath);
            Assert.Single(
                integrationFact.AttributeLists.SelectMany(static list => list.Attributes),
                static attribute => attribute.Name.ToString() is "Fact" or "FactAttribute");
            Assert.DoesNotContain(
                fastClass.Members.OfType<MethodDeclarationSyntax>(),
                method => method.Identifier.ValueText == factName);
        }

        Assert.DoesNotContain(
            fastClass.DescendantNodes().OfType<ObjectCreationExpressionSyntax>(),
            static creation => creation.Type.ToString() == "FileSystemManager");
        Assert.DoesNotContain(
            fastClass.DescendantNodes().OfType<NameSyntax>(),
            static name => name.ToString() == "PhysicalLoadTransactionOperations");
        Assert.DoesNotContain(
            fastClass.DescendantNodes().OfType<InvocationExpressionSyntax>(),
            static invocation => ReadInvocationName(invocation) is
                "LoadAsync" or
                "WriteFileAtomicAsync" or
                "AcquireCanonicalWriteLeaseAsync" or
                "EnsureDirectoryStructure" or
                "GetTempPath");
    }

    private static void InspectSource(
        string relativePath,
        string source,
        IReadOnlyDictionary<InventoryOwner, int> allowedContributionWriters,
        IDictionary<InventoryOwner, int> contributionCounts,
        IReadOnlyDictionary<InventoryOwner, int> allowedMissingScopeMutations,
        IDictionary<InventoryOwner, int> removalCounts,
        ICollection<string> violations)
    {
        var root = CSharpSyntaxTree.ParseText(source, path: relativePath)
            .GetCompilationUnitRoot();
        foreach (var diagnostic in root.GetDiagnostics().Where(static diagnostic =>
                     diagnostic.Severity == DiagnosticSeverity.Error))
        {
            violations.Add($"{relativePath}: C# parse error {diagnostic}");
        }

        foreach (var assignment in root.DescendantNodes()
                     .OfType<AssignmentExpressionSyntax>())
        {
            if (TryReadElementKey(assignment.Left, out var key) &&
                string.Equals(key, "contribution", StringComparison.Ordinal))
            {
                RecordAllowedOccurrence(
                    assignment,
                    relativePath,
                    allowedContributionWriters,
                    contributionCounts,
                    "direct contribution writer",
                    violations);
            }

            if (TryReadElementKey(assignment.Left, out key) &&
                string.Equals(key, "scope", StringComparison.Ordinal) &&
                assignment.Right.IsKind(SyntaxKind.NullLiteralExpression))
            {
                violations.Add(Describe(
                    assignment,
                    relativePath,
                    "scope cannot be replaced with null outside a semantic negative factory"));
            }
        }

        foreach (var invocation in root.DescendantNodes()
                     .OfType<InvocationExpressionSyntax>())
        {
            if (TryReadAddedKey(invocation, out var addedKey) &&
                string.Equals(addedKey, "contribution", StringComparison.Ordinal))
            {
                RecordAllowedOccurrence(
                    invocation,
                    relativePath,
                    allowedContributionWriters,
                    contributionCounts,
                    "direct contribution Add/TryAdd writer",
                    violations);
            }

            if (IsLiteralRemove(invocation, "scope"))
            {
                RecordAllowedOccurrence(
                    invocation,
                    relativePath,
                    allowedMissingScopeMutations,
                    removalCounts,
                    "unlisted Remove(\"scope\") mutation",
                    violations);
            }
        }

        foreach (var initializer in root.DescendantNodes()
                     .OfType<InitializerExpressionSyntax>()
                     .Where(static initializer => initializer.IsKind(
                         SyntaxKind.ComplexElementInitializerExpression)))
        {
            if (initializer.Expressions.FirstOrDefault() is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression) &&
                string.Equals(
                    literal.Token.ValueText,
                    "contribution",
                    StringComparison.Ordinal))
            {
                RecordAllowedOccurrence(
                    initializer,
                    relativePath,
                    allowedContributionWriters,
                    contributionCounts,
                    "direct complex-initializer contribution writer",
                    violations);
            }
        }

        foreach (var expression in root.DescendantNodes()
                     .OfType<ExpressionSyntax>())
        {
            if (!TryReadConstantString(expression, out var value) ||
                IsNestedConstantStringExpression(expression) ||
                !ContainsRollPayloadObject(value))
            {
                continue;
            }

            violations.Add(Describe(
                expression,
                relativePath,
                "direct JSON-string roll payload bypasses the semantic fixture factory"));
        }

        foreach (var interpolated in root.DescendantNodes()
                     .OfType<InterpolatedStringExpressionSyntax>())
        {
            var decodedText = DecodeInterpolatedText(interpolated);
            if (decodedText.Contains("\"operations\"", StringComparison.Ordinal) &&
                decodedText.Contains("\"contribution\"", StringComparison.Ordinal))
            {
                violations.Add(Describe(
                    interpolated,
                    relativePath,
                    "interpolated JSON roll payload bypasses the semantic fixture factory"));
            }
        }
    }

    private static void AssertViolationDetected(
        string source,
        string expectedDescription)
    {
        var violations = new List<string>();
        InspectSource(
            "RegressionProbe.cs",
            source,
            new Dictionary<InventoryOwner, int>(),
            new Dictionary<InventoryOwner, int>(),
            new Dictionary<InventoryOwner, int>(),
            new Dictionary<InventoryOwner, int>(),
            violations);

        var violation = Assert.Single(violations);
        Assert.Contains(expectedDescription, violation, StringComparison.Ordinal);
    }

    private static bool TryReadConstantString(
        ExpressionSyntax expression,
        out string value)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal
                when literal.IsKind(SyntaxKind.StringLiteralExpression):
                value = literal.Token.ValueText;
                return true;
            case ParenthesizedExpressionSyntax parenthesized:
                return TryReadConstantString(parenthesized.Expression, out value);
            case BinaryExpressionSyntax binary
                when binary.IsKind(SyntaxKind.AddExpression) &&
                     TryReadConstantString(binary.Left, out var left) &&
                     TryReadConstantString(binary.Right, out var right):
                value = left + right;
                return true;
            default:
                value = string.Empty;
                return false;
        }
    }

    private static bool IsNestedConstantStringExpression(
        ExpressionSyntax expression) =>
        expression.Parent is ExpressionSyntax parent &&
        TryReadConstantString(parent, out _);

    private static string DecodeInterpolatedText(
        InterpolatedStringExpressionSyntax interpolated) =>
        string.Concat(interpolated.Contents.Select(static content => content switch
        {
            InterpolatedStringTextSyntax text => text.TextToken.ValueText,
            InterpolationSyntax => "__interpolation__",
            _ => string.Empty
        }));

    private static bool TryReadElementKey(ExpressionSyntax expression, out string key)
    {
        SeparatedSyntaxList<ArgumentSyntax> arguments;
        switch (expression)
        {
            case ElementAccessExpressionSyntax elementAccess:
                arguments = elementAccess.ArgumentList.Arguments;
                break;
            case ImplicitElementAccessSyntax implicitAccess:
                arguments = implicitAccess.ArgumentList.Arguments;
                break;
            default:
                key = string.Empty;
                return false;
        }

        if (arguments.Count == 1 &&
            arguments[0].Expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            key = literal.Token.ValueText;
            return true;
        }

        key = string.Empty;
        return false;
    }

    private static bool TryReadAddedKey(
        InvocationExpressionSyntax invocation,
        out string key)
    {
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => string.Empty
        };
        var arguments = invocation.ArgumentList.Arguments;
        if (name is "Add" or "TryAdd" &&
            arguments.Count > 0 &&
            arguments[0].Expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            key = literal.Token.ValueText;
            return true;
        }

        key = string.Empty;
        return false;
    }

    private static string ReadInvocationName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => string.Empty
        };

    private static bool IsLiteralRemove(
        InvocationExpressionSyntax invocation,
        string expectedKey)
    {
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => string.Empty
        };
        var arguments = invocation.ArgumentList.Arguments;
        return string.Equals(name, "Remove", StringComparison.Ordinal) &&
               arguments.Count > 0 &&
               arguments[0].Expression is LiteralExpressionSyntax literal &&
               literal.IsKind(SyntaxKind.StringLiteralExpression) &&
               string.Equals(
                   literal.Token.ValueText,
                   expectedKey,
                   StringComparison.Ordinal);
    }

    private static bool ContainsRollPayloadObject(string value)
    {
        if (!value.Contains("contribution", StringComparison.Ordinal) ||
            !value.Contains("operations", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            return ContainsRollPayloadObject(JsonNode.Parse(value));
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static bool ContainsRollPayloadObject(JsonNode? node)
    {
        if (node is JsonObject payloadObject &&
            payloadObject.ContainsKey("operations") &&
            payloadObject.ContainsKey("contribution"))
        {
            return true;
        }

        return node switch
        {
            JsonObject objectValue => objectValue.Any(static pair =>
                ContainsRollPayloadObject(pair.Value)),
            JsonArray arrayValue => arrayValue.Any(ContainsRollPayloadObject),
            _ => false
        };
    }

    private static void RecordAllowedOccurrence(
        SyntaxNode node,
        string relativePath,
        IReadOnlyDictionary<InventoryOwner, int> allowed,
        IDictionary<InventoryOwner, int> counts,
        string description,
        ICollection<string> violations)
    {
        var owner = ReadOwner(node, relativePath);
        if (!allowed.ContainsKey(owner))
        {
            violations.Add(Describe(node, relativePath, description));
            return;
        }

        counts[owner]++;
    }

    private static InventoryOwner ReadOwner(
        SyntaxNode node,
        string relativePath)
    {
        var type = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        var member = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        return new InventoryOwner(
            relativePath,
            type?.Identifier.ValueText ?? "<no-type>",
            member?.Identifier.ValueText ?? "<no-method>");
    }

    private static void AssertExactInventory(
        string description,
        IReadOnlyDictionary<InventoryOwner, int> expected,
        IReadOnlyDictionary<InventoryOwner, int> actual,
        ICollection<string> violations)
    {
        foreach (var (owner, expectedCount) in expected)
        {
            var actualCount = actual.GetValueOrDefault(owner);
            if (actualCount != expectedCount)
            {
                violations.Add(
                    $"{owner.RelativePath}:{owner.TypeName}.{owner.MemberName} " +
                    $"must contain exactly {expectedCount} {description}(s), " +
                    $"found {actualCount}");
            }
        }
    }

    private static string Describe(
        SyntaxNode node,
        string relativePath,
        string description)
    {
        var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var owner = ReadOwner(node, relativePath);
        return $"{relativePath}:{line} {owner.TypeName}.{owner.MemberName}: {description}";
    }

    private static IEnumerable<(string RelativePath, string Source)>
        EnumerateExecutableCSharpSources()
    {
        foreach (var project in new[]
                 {
                     "BookOfEternityClient.Tests",
                     "BookOfEternityClient.IntegrationTests",
                     "BookOfEternityClient.TestSupport"
                 })
        {
            var root = Path.Combine(TestRepoPaths.RepoRoot, project);
            foreach (var path in Directory.EnumerateFiles(
                         root,
                         "*.cs",
                         SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(
                        TestRepoPaths.RepoRoot,
                        path)
                    .Replace('\\', '/');
                if (relativePath.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
                    relativePath.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return (relativePath, File.ReadAllText(path));
            }
        }
    }
}
