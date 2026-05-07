namespace TestMap.Services.TestGeneration.Validation;

public enum GenerationValidationFailureClass
{
    MalformedGeneratedCode,
    BadInsertion,
    Infrastructure,
    PreExistingDiagnostics,
    CompilerSemantic,
    Runtime,
    Assertion
}
