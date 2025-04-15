using JetBrains.Annotations;
using TestMap.Models;
using TestMap.Models.Code;
using Xunit;

namespace TestMap.Tests.UnitTests.Models;

[TestSubject(typeof(TestClass))]
public class TestClassTest
{
    private readonly string _bodyEndPosition;
    private readonly string _bodyStartPosition;
    private readonly string _classBody;
    private readonly string _classDeclaration;
    private readonly string _classFields;
    private readonly string _filePath;
    private readonly string _languageFramework;
    private readonly string _ns;
    private readonly string _owner;
    private readonly string _projectPath;
    private readonly string _repo;
    private readonly string _solutionFilePath;
    private readonly string _sourceBody;
    private readonly string _testFramework;
    private readonly string _usingStatements;

    public TestClassTest()
    {
        _owner = "owner";
        _repo = "repo";
        _solutionFilePath = "solution.sln";
        _projectPath = "example.csproj";
        _filePath = "class.cs";
        _ns = "Example.ClassTest";
        _classDeclaration = "public class ClassTest";
        _classFields = "";
        _usingStatements = "using System";
        _testFramework = "xUnit";
        _languageFramework = "8.0";
        _classBody =
            "public class ClassTest { [Fact] public void TestMethod1_Should_Do_Something() { //act //arrange //assert } }";
        _bodyStartPosition = "21";
        _bodyEndPosition = "430";
        _sourceBody = "public class Class { public int Method1() => 42; }";
    }

    private TestClass CreateTestClassRecord()
    {
        return new TestClass(_owner, _repo, _solutionFilePath, _projectPath, _filePath,
            _ns, _classDeclaration, _classFields, _usingStatements, _testFramework, _languageFramework,
            _classBody, _bodyStartPosition, _bodyEndPosition, _sourceBody);
    }

    [Fact]
    [Trait("Category", "CI")]
    public void Constructor_ShouldInitializeTestClassRecord()
    {
        var record = CreateTestClassRecord();

        Assert.Equal(_owner, record.Owner);
        Assert.Equal(_repo, record.Repo);
        Assert.Equal(_solutionFilePath, record.SolutionFilePath);
        Assert.Equal(_projectPath, record.ProjectPath);
        Assert.Equal(_filePath, record.FilePath);
        Assert.Equal(_ns, record.Namespace);
        Assert.Equal(_classDeclaration, record.ClassDeclaration);
        Assert.Equal(_classFields, record.ClassFields);
        Assert.Equal(_usingStatements, record.UsingStatements);
        Assert.Equal(_testFramework, record.TestFramework);
        Assert.Equal(_languageFramework, record.LanguageFramework);
        Assert.Equal(_classBody, record.ClassBody);
        Assert.Equal(_bodyStartPosition, record.BodyStartPosition);
        Assert.Equal(_bodyEndPosition, record.BodyEndPosition);
        Assert.Equal(_sourceBody, record.SourceBody);
    }
}