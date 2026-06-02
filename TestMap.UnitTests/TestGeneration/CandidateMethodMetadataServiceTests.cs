using TestMap.Services.TestGeneration.TargetSelection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace TestMap.UnitTests.TestGeneration;

public sealed class CandidateMethodMetadataServiceTests
{
    /// <summary>
    /// Verifies that branch and guard syntax is translated into generation-oriented test intentions.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Build_WithBranchingMethod_ReturnsTestIntentions()
    {
        var service = new CandidateMethodMetadataService();
        const string methodSource = """
                                    public int Calculate(string? value, int amount)
                                    {
                                        if (value is null)
                                        {
                                            throw new ArgumentNullException(nameof(value));
                                        }

                                        if (amount <= 0)
                                        {
                                            return 0;
                                        }

                                        return value.Length > amount ? amount : value.Length;
                                    }
                                    """;

        var metadata = service.Build(methodSource, "public class Calculator { public Calculator() {} }");

        Assert.Contains(metadata.TestIntentions, x => x.Kind is "NullGuard" or "NullGuardThrowPath");
        Assert.Contains(metadata.TestIntentions, x =>
            x.Kind == "Branch" && x.SourceText.Contains("amount <= 0") && x.Intention.Contains("true"));
        Assert.Contains(metadata.TestIntentions, x =>
            x.Kind == "Branch" && x.SourceText.Contains("amount <= 0") && x.Intention.Contains("false"));
        Assert.Contains(metadata.TestIntentions, x => x.Kind == "ConditionalExpression" && x.Intention.Contains("true"));
        Assert.Contains(metadata.TestIntentions, x => x.Kind == "ConditionalExpression" && x.Intention.Contains("false"));
        Assert.Contains("Tests that", metadata.TestIntentionsSummary);
    }

    /// <summary>
    /// Verifies that constructor dependencies and method parameters receive deterministic construction guidance.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Build_WithConstructorAndParameters_ReturnsTypeConstructionGuidance()
    {
        var service = new CandidateMethodMetadataService();
        const string classSource = """
                                   public class OrderService
                                   {
                                       public OrderService(IOrderRepository repository, Clock clock) {}
                                       public bool Submit(Order order, CancellationToken cancellationToken) => true;
                                   }
                                   """;
        const string methodSource = "public bool Submit(Order order, CancellationToken cancellationToken) => true;";

        var metadata = service.Build(methodSource, classSource);

        Assert.Equal("OrderService", metadata.TypeConstruction.SutTypeName);
        Assert.Contains(metadata.TypeConstruction.SutDependencies, x =>
            x.Name == "repository" && x.Strategy == "mock");
        Assert.Contains(metadata.TypeConstruction.MethodParameters, x =>
            x.Name == "cancellationToken" && x.Strategy == "primitive-or-default");
        Assert.Contains("SUT: OrderService", metadata.TypeConstructionSummary);
    }

    /// <summary>
    /// Verifies that nullable-access expressions (the ?. operator) produce a NullableAccess intention
    /// so that the LLM knows to cover the null-receiver case.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Build_WithNullableAccessExpression_ReturnsNullableAccessIntention()
    {
        var service = new CandidateMethodMetadataService();
        const string methodSource = """
                                    public int? GetCount(MyCollection? collection)
                                    {
                                        return collection?.Items?.Count;
                                    }
                                    """;

        var metadata = service.Build(methodSource, "public class Svc {}");

        Assert.Contains(metadata.TestIntentions, x =>
            x.Kind == "NullableAccess" &&
            x.SourceText.Contains("collection"));
        Assert.Contains("nullable access", metadata.TestIntentionsSummary,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that semantic type resolution recurses through concrete constructors and collection element types.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task BuildAsync_WithRoslynDocument_ReturnsRecursiveTypeConstructionGuidance()
    {
        var service = new CandidateMethodMetadataService();
        const string source = """
                              using System.Collections.Generic;
                              using System.Threading;

                              public interface IOrderRepository {}

                              public sealed class Money
                              {
                                  public Money(decimal amount) {}
                              }

                              public sealed class Order
                              {
                                  public Order(Money total) {}
                              }

                              public sealed class OrderService
                              {
                                  public OrderService(IOrderRepository repository) {}

                                  public bool Submit(Order order, IReadOnlyList<Order> related, CancellationToken cancellationToken)
                                  {
                                      return related.Count > 0;
                                  }
                              }
                              """;
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "Source",
                "Source",
                LanguageNames.CSharp,
                metadataReferences:
                [
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(IReadOnlyList<>).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(CancellationToken).Assembly.Location)
                ],
                parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)))
            .AddDocument(documentId, "OrderService.cs", SourceText.From(source));
        var document = solution.GetDocument(documentId);
        var root = await document!.GetSyntaxRootAsync();
        var method = root!.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .Single(x => x.Identifier.Text == "Submit");

        var metadata = await service.BuildAsync(
            document,
            method.SpanStart,
            method.Span.End,
            method.ToString(),
            method.FirstAncestorOrSelf<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()!.ToString());

        Assert.Contains(metadata.TypeConstruction.SutDependencies, x =>
            x.Name == "repository" && x.Strategy == "mock");

        var orderParameter = Assert.Single(metadata.TypeConstruction.MethodParameters, x => x.Name == "order");
        Assert.Equal("construct", orderParameter.Strategy);
        var total = Assert.Single(orderParameter.Children, x => x.Name == "total");
        Assert.Equal("construct", total.Strategy);
        Assert.Contains(total.Children, x => x.Name == "amount" && x.Strategy == "primitive-or-default");

        var relatedParameter = Assert.Single(metadata.TypeConstruction.MethodParameters, x => x.Name == "related");
        Assert.Equal("collection", relatedParameter.Strategy);
        Assert.Contains(relatedParameter.Children, x => x.Name == "item" && x.TypeName == "Order");
    }

    /// <summary>
    /// Verifies that static methods accepting System.Type produce branch-oriented type-token guidance
    /// instead of invalid constructor guidance such as new Type() or new static SUT instances.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Build_WithStaticTypeParameter_ReturnsStaticAndTypeTokenGuidance()
    {
        var service = new CandidateMethodMetadataService();
        const string methodSource = """
                                    public static AspectPredicate Implement(Type baseOrInterfaceType)
                                    {
                                        if (baseOrInterfaceType == null)
                                        {
                                            throw new ArgumentNullException(nameof(baseOrInterfaceType));
                                        }

                                        if (baseOrInterfaceType.IsSealed)
                                        {
                                            throw new ArgumentException("The base type is not allowed to be Sealed.");
                                        }

                                        return methodInfo => baseOrInterfaceType.IsAssignableFrom(methodInfo.DeclaringType);
                                    }
                                    """;
        const string classSource = "public static class Predicates {}";

        var metadata = service.Build(methodSource, classSource);

        Assert.False(metadata.TypeConstruction.RequiresSutInstance);
        Assert.Contains("static call target Predicates", metadata.TypeConstructionSummary);
        Assert.DoesNotContain("construct Type", metadata.TypeConstructionSummary);

        var parameter = Assert.Single(metadata.TypeConstruction.MethodParameters, x => x.Name == "baseOrInterfaceType");
        Assert.Equal("type-token", parameter.Strategy);
        Assert.Contains("typeof", parameter.Summary);
        Assert.Contains(metadata.TestIntentions, x =>
            x.Kind == "NullGuardThrowPath" &&
            x.Intention.Contains("ArgumentNullException") &&
            x.Intention.Contains("baseOrInterfaceType"));
        Assert.Contains(metadata.TestIntentions, x =>
            x.Kind == "GuardedThrowPath" &&
            x.Intention.Contains("ArgumentException") &&
            x.Intention.Contains("base type is not allowed"));
    }
}
