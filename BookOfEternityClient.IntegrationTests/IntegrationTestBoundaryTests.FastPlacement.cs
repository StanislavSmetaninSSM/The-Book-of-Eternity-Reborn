using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Xml.Linq;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class IntegrationTestBoundaryTests
{
    [Fact]
    public void MixedFastPlacement_PreservesAllSixtySixReviewedCasesExactlyOnce()
    {
        var contracts = new[]
        {
            (ClassName: "MortalItemConsumptionPlannerTests", FastCount: 40, IntegrationCount: 4,
                Fast: FastPlacementTestInventories.MortalItemConsumptionPlannerTestsFast,
                Integration: FastPlacementTestInventories.MortalItemConsumptionPlannerTestsIntegration),
            (ClassName: "ShiningAbodeTradeAndForgeStateTests", FastCount: 8, IntegrationCount: 1,
                Fast: FastPlacementTestInventories.ShiningAbodeTradeAndForgeStateTestsFast,
                Integration: FastPlacementTestInventories.ShiningAbodeTradeAndForgeStateTestsIntegration),
            (ClassName: "GmWorkerAuditLogTests", FastCount: 2, IntegrationCount: 11,
                Fast: FastPlacementTestInventories.GmWorkerAuditLogTestsFast,
                Integration: FastPlacementTestInventories.GmWorkerAuditLogTestsIntegration)
        };
        foreach (var contract in contracts)
        {
            var fast = ManifestLines(contract.Fast);
            var integration = ManifestLines(contract.Integration);
            Assert.Equal(contract.FastCount, fast.Length);
            Assert.Equal(contract.IntegrationCount, integration.Length);
            Assert.Empty(fast.Intersect(integration, StringComparer.Ordinal));
            foreach (var part in new[]
            {
                (Directory: FastTestsDirectory, File: contract.ClassName + ".cs", Rows: fast),
                (Directory: IntegrationTestsDirectory, File: contract.ClassName + ".Session.cs", Rows: integration)
            })
            {
                var violations = ExactTestInventoryViolations(
                    SourcePath(part.Directory, part.File), contract.ClassName, part.Rows);
                Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
            }
        }
        Assert.Equal(50, contracts.Sum(contract => contract.FastCount));
        Assert.Equal(16, contracts.Sum(contract => contract.IntegrationCount));
    }

    /// <summary>
    /// Verifies that the shared linked item fixture is an abstract helper-only base and both test leaves remain owned by their source roots.
    /// </summary>
    [Fact]
    public void MixedFastPlacement_LinksOnlyTheReviewedHelperFixtureBase()
    {
        const string helper = "MortalItemConsumptionPlannerTests.Helpers.cs";
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(
            SourcePath(FastTestsDirectory, helper))).GetCompilationUnitRoot();
        Assert.DoesNotContain(root.GetDiagnostics(), diagnostic =>
            diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.DoesNotContain(root.DescendantNodes().OfType<AttributeSyntax>(), attribute =>
            AttributeNameIs(attribute, "Fact") || AttributeNameIs(attribute, "Theory") ||
            AttributeNameIs(attribute, "InlineData") || AttributeNameIs(attribute, "Trait"));
        var fixtureBase = Assert.Single(root.Members.OfType<FileScopedNamespaceDeclarationSyntax>())
            .Members.OfType<ClassDeclarationSyntax>().Single();
        Assert.Equal("MortalItemConsumptionPlannerTestFixture", fixtureBase.Identifier.ValueText);
        Assert.Contains(fixtureBase.Modifiers, modifier => modifier.IsKind(SyntaxKind.AbstractKeyword));
        Assert.DoesNotContain(fixtureBase.Modifiers, modifier => modifier.IsKind(SyntaxKind.PartialKeyword));
        foreach (var path in new[]
        {
            SourcePath(FastTestsDirectory, "MortalItemConsumptionPlannerTests.cs"),
            SourcePath(IntegrationTestsDirectory, "MortalItemConsumptionPlannerTests.Session.cs")
        })
        {
            var leaf = Assert.Single(CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot()
                .DescendantNodes().OfType<ClassDeclarationSyntax>(), declaration =>
                    declaration.Identifier.ValueText == "MortalItemConsumptionPlannerTests");
            Assert.DoesNotContain(leaf.Modifiers, modifier => modifier.IsKind(SyntaxKind.PartialKeyword));
            Assert.Equal("MortalItemConsumptionPlannerTestFixture",
                Assert.Single(leaf.BaseList!.Types).Type.ToString());
        }
        var project = XDocument.Load(SourcePath(IntegrationTestsDirectory,
            "BookOfEternityClient.IntegrationTests.csproj"));
        var link = Assert.Single(project.Descendants("Compile"), element =>
            string.Equals(element.Element("Link")?.Value ?? element.Attribute("Link")?.Value,
                helper, StringComparison.Ordinal));
        Assert.Equal("../BookOfEternityClient.Tests/" + helper,
            link.Attribute("Include")!.Value.Replace('\\', '/'));
        Assert.DoesNotContain(project.Descendants("Compile"), element =>
            new[] { "MortalItemConsumptionPlannerTests.cs", "ShiningAbodeTradeAndForgeStateTests.cs",
                "GmWorkerAuditLogTests.cs" }.Any(file =>
                (element.Attribute("Include")?.Value.Replace('\\', '/') ?? "")
                    .EndsWith("/" + file, StringComparison.Ordinal)));
    }
}
