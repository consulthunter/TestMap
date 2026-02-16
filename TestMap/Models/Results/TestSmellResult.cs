namespace TestMap.Models.Results;

public class TestSmellResult
{
    public string Name { get; set; }
    public string Message { get; set; }
    public List<MethodSmellResult> Methods { get; set; }
}

public class MethodSmellResult
{
    public string Name { get; set; }
    public string Body { get; set; }
    public List<TestSmell> Smells { get; set; }
}

public class TestSmell
{
    public string Name { get; set; }
    public string Status { get; set; }
}