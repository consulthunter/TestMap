namespace TestMap.Models;

public class TestClassRecord
{
    public string Repo;
    public string FilePath;
    public string Namespace;
    public string ClassDeclaration;
    public List<string> ClassFields;
    public List<string> UsingStatements;
    public string Framework;
    public string ClassBody;
    public string SourceBody;
    
    public TestClassRecord(
        string repo,
        string filePath,
        string ns,
        string classDeclaration,
        List<string> classFields = null,
        List<string> usingStatements = null,
        string framework = null,
        string classBody = null,
        string sourceBody = null)
    {
        Repo = repo;
        FilePath = filePath;
        Namespace = ns;
        ClassDeclaration = classDeclaration;
        ClassFields = classFields ?? new List<string>();
        UsingStatements = usingStatements ?? new List<string>();
        Framework = framework;
        ClassBody = classBody;
        SourceBody = sourceBody;
    }
}