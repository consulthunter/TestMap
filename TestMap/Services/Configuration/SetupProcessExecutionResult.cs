namespace TestMap.Services.Configuration;

public sealed record SetupProcessExecutionResult(int ExitCode, string StdOut, string StdErr);
