namespace TestMap.Models;

public class TestMethod
{
    public string Repo;
    public string FilePath;
    public string Namespace;
    public string ClassDeclaration;
    public List<string> ClassFields;
    public List<string> UsingStatemenets;
    public string Framework;
    public string MethodBody;
    public List<(string, string)> MethodInvocations;
}