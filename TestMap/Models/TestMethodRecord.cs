namespace TestMap.Models;

public class TestMethodRecord
{
    public string Repo;
    public string FilePath;
    public string Namespace;
    public string ClassDeclaration;
    public List<string> ClassFields;
    public List<string> UsingStatements;
    public string Framework;
    public string MethodBody;
    public List<(string, string)> MethodInvocations;
    
    public TestMethodRecord(
        string repo,
        string filePath,
        string ns,
        string classDeclaration,
        List<string> classFields = null,
        List<string> usingStatements = null,
        string framework = null,
        string methodBody = null,
        List<(string, string)> methodInvocations = null)
    {
        Repo = repo;
        FilePath = filePath;
        Namespace = ns;
        ClassDeclaration = classDeclaration;
        ClassFields = classFields ?? new List<string>();
        UsingStatements = usingStatements ?? new List<string>();
        Framework = framework;
        MethodBody = methodBody;
        MethodInvocations = methodInvocations ?? new List<(string, string)>();
    }
}