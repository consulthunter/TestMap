using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace TestMap.Services.StaticAnalysis.Assertions;

public sealed record AssertionOperand(
    int Index,
    string Role,
    IOperation Operation,
    bool IsExecution);

public sealed class AssertionOperandExtractor
{
    public IReadOnlyList<AssertionOperand> Extract(
        IInvocationOperation invocation,
        AssertionPatternMatch pattern)
    {
        if (!pattern.IsSupported) return [];

        var operands = pattern.OperandShape switch
        {
            AssertionOperandShape.FluentReceiver => ExtractFluent(invocation),
            AssertionOperandShape.ShouldlyReceiver => ExtractShouldly(invocation),
            AssertionOperandShape.ExceptionDelegate => ExtractException(invocation),
            AssertionOperandShape.NUnitConstraint => ExtractArguments(invocation),
            AssertionOperandShape.StaticArguments => ExtractArguments(invocation),
            _ => []
        };

        return operands
            .Select((operand, index) => operand with { Index = index })
            .ToList();
    }

    /// <summary>
    /// Extracts operands when the assertion terminal itself has no bound symbol — typically
    /// because the test framework arrives from a package the compilation does not carry. The
    /// terminal is only a marker; what matters is where its operands come from, and operands
    /// referring to production code bind through the project reference and stay analyzable.
    /// Mirrors the source-test tracer, which steps over what it cannot resolve rather than
    /// discarding the whole member.
    /// </summary>
    public IReadOnlyList<AssertionOperand> ExtractFromSyntax(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        var operands = new List<AssertionOperand>();

        // An instance receiver is an operand (actual.ShouldBe(1)); a type receiver is not
        // (Assert.IsTrue(...)). An unbound type name yields no usable operation, so resolving
        // the receiver is enough to tell the two apart.
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            CollectResolvable(memberAccess.Expression, semanticModel, false, operands);

        foreach (var argument in invocation.ArgumentList.Arguments)
            CollectResolvable(argument.Expression, semanticModel, true, operands);

        return operands
            .Select((operand, index) => operand with { Index = index })
            .ToList();
    }

    /// <summary>
    /// Adds the outermost sub-expressions the semantic model can bind. An unbound expression
    /// is descended into rather than dropped, so a constraint such as Is.EqualTo(42) still
    /// contributes its literal even though the constraint call does not bind.
    /// </summary>
    private static void CollectResolvable(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        bool allowDescent,
        List<AssertionOperand> operands)
    {
        var operation = semanticModel.GetOperation(expression);
        if (operation is not null and not IInvalidOperation)
        {
            operands.Add(new AssertionOperand(
                0,
                "argument",
                operation,
                Unwrap(operation) is IAnonymousFunctionOperation or IDelegateCreationOperation));
            return;
        }

        if (!allowDescent) return;

        foreach (var child in expression.ChildNodes().OfType<ExpressionSyntax>())
            CollectResolvable(child, semanticModel, true, operands);
    }

    private static List<AssertionOperand> ExtractArguments(IInvocationOperation invocation)
    {
        return invocation.Arguments
            .Where(IsValueBearing)
            .Select(argument => new AssertionOperand(
                0,
                ResolveRole(argument),
                argument.Value,
                IsDelegate(argument)))
            .ToList();
    }

    private static List<AssertionOperand> ExtractException(IInvocationOperation invocation)
    {
        var result = new List<AssertionOperand>();
        foreach (var argument in invocation.Arguments.Where(IsValueBearing))
        {
            var execution = IsDelegate(argument);
            result.Add(new AssertionOperand(
                0,
                execution ? "exceptionDelegate" : ResolveRole(argument),
                argument.Value,
                execution));
        }

        var receiver = FindFluentSubject(invocation);
        if (receiver != null)
            result.Insert(0, new AssertionOperand(0, "subject", receiver, true));
        return result;
    }

    private static List<AssertionOperand> ExtractFluent(IInvocationOperation invocation)
    {
        var result = new List<AssertionOperand>();
        var subject = FindFluentSubject(invocation);
        if (subject != null)
            result.Add(new AssertionOperand(0, "subject", subject, false));

        result.AddRange(invocation.Arguments
            .Where(IsValueBearing)
            .Select(argument => new AssertionOperand(
                0,
                ResolveRole(argument),
                argument.Value,
                IsDelegate(argument))));
        return result;
    }

    private static List<AssertionOperand> ExtractShouldly(IInvocationOperation invocation)
    {
        var result = new List<AssertionOperand>();
        var receiver = Unwrap(invocation.Instance);
        if (receiver != null)
            result.Add(new AssertionOperand(0, "actual", receiver, false));

        result.AddRange(invocation.Arguments
            .Where(IsValueBearing)
            .Select(argument => new AssertionOperand(
                0,
                ResolveRole(argument),
                argument.Value,
                IsDelegate(argument))));
        return result;
    }

    private static IOperation? FindFluentSubject(IInvocationOperation terminal)
    {
        IOperation? current = Unwrap(terminal.Instance);
        while (current is IInvocationOperation invocation)
        {
            if (invocation.TargetMethod.Name == "Should")
                return Unwrap(invocation.Instance) ??
                       invocation.Arguments.FirstOrDefault()?.Value;
            current = Unwrap(invocation.Instance);
        }

        return null;
    }

    private static bool IsValueBearing(IArgumentOperation argument)
    {
        var name = argument.Parameter?.Name ?? string.Empty;
        return !name.Contains("message", StringComparison.OrdinalIgnoreCase) &&
               !name.Equals("userMessage", StringComparison.OrdinalIgnoreCase) &&
               !name.Equals("because", StringComparison.OrdinalIgnoreCase) &&
               !name.Equals("becauseArgs", StringComparison.OrdinalIgnoreCase) &&
               !name.Equals("format", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveRole(IArgumentOperation argument)
    {
        return argument.Parameter?.Name ?? "argument";
    }

    private static bool IsDelegate(IArgumentOperation argument)
    {
        return argument.Parameter?.Type.TypeKind == TypeKind.Delegate ||
               Unwrap(argument.Value) is IAnonymousFunctionOperation or IDelegateCreationOperation;
    }

    private static IOperation? Unwrap(IOperation? operation)
    {
        while (operation is IConversionOperation conversion)
            operation = conversion.Operand;
        return operation;
    }
}
