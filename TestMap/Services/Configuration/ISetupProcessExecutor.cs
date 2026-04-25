namespace TestMap.Services.Configuration;

public interface ISetupProcessExecutor
{
    SetupProcessExecutionResult Run(string fileName, string arguments, bool throwOnFailure);
    void Start(string fileName, string arguments, bool useShellExecute);
}
