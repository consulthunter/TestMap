using System.Collections.Generic;
using System.Text.Json;
using JetBrains.Annotations;
using TestMap.Models.Code;
using Xunit;

namespace TestMap.Tests.UnitTests.Models.Code;

[TestSubject(typeof(FileModel))]
public class FileModelTest
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues_WhenNoArgumentsProvided()
    {
        // Arrange
        var defaultUsingStatements = new List<string>();

        // Act
        var fileModel = new FileModel(defaultUsingStatements);

        // Assert
        Assert.NotNull(fileModel);
        Assert.Equal(0, fileModel.Id);
        Assert.Equal(0, fileModel.AnalysisProjectId);
        Assert.Equal("", fileModel.Guid);
        Assert.Equal("", fileModel.Namespace);
        Assert.Equal("", fileModel.Name);
        Assert.Equal("", fileModel.Language);
        Assert.Equal("", fileModel.FilePath);
        Assert.NotNull(fileModel.UsingStatements);
        Assert.NotNull(fileModel.MetaData);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithProvidedValues()
    {
        // Arrange
        var usingStatements = new List<string> { "using System;", "using System.Text;" };
        var packageId = 1;
        var guid = "abc-123";
        var ns = "TestNamespace";
        var name = "FileName.cs";
        var language = "C#";
        var solutionFilePath = @"C:\Projects\Solution.sln";
        var projectPath = @"C:\Projects\TestMap.csproj";
        var filePath = @"C:\Projects\FileName.cs";

        // Act
        var fileModel = new FileModel(usingStatements, packageId, guid, ns, name, language, solutionFilePath,
            projectPath, filePath);

        // Assert
        Assert.NotNull(fileModel);
        Assert.Equal(packageId, fileModel.AnalysisProjectId);
        Assert.Equal(guid, fileModel.Guid);
        Assert.Equal(ns, fileModel.Namespace);
        Assert.Equal(name, fileModel.Name);
        Assert.Equal(language, fileModel.Language);
        Assert.Equal(filePath, fileModel.FilePath);
        Assert.Equal(JsonSerializer.Serialize(usingStatements), fileModel.UsingStatements);
        var metaData = JsonSerializer.Deserialize<Dictionary<string, string>>(fileModel.MetaData);
        Assert.Equal(solutionFilePath, metaData?["SolutionFilePath"]);
        Assert.Equal(projectPath, metaData?["ProjectFilePath"]);
    }

    [Fact]
    public void Id_ShouldAllowGetAndSet()
    {
        // Arrange
        var fileModel = new FileModel(new List<string>());
        var expectedId = 42;

        // Act
        fileModel.Id = expectedId;

        // Assert
        Assert.Equal(expectedId, fileModel.Id);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(0)]
    [InlineData(-1)]
    public void PackageId_ShouldAllowGetAndSet(int packageId)
    {
        // Arrange
        var fileModel = new FileModel(new List<string>());

        // Act
        fileModel.AnalysisProjectId = packageId;

        // Assert
        Assert.Equal(packageId, fileModel.AnalysisProjectId);
    }

    [Theory]
    [InlineData("abc-123")]
    [InlineData("xyz-987")]
    [InlineData("")]
    public void Guid_ShouldAllowGetAndSet(string guid)
    {
        // Arrange
        var fileModel = new FileModel(new List<string>());

        // Act
        fileModel.Guid = guid;

        // Assert
        Assert.Equal(guid, fileModel.Guid);
    }

    [Theory]
    [InlineData("TestNamespace")]
    [InlineData("")]
    public void Namespace_ShouldAllowGetAndSet(string ns)
    {
        // Arrange
        var fileModel = new FileModel(new List<string>());

        // Act
        fileModel.Namespace = ns;

        // Assert
        Assert.Equal(ns, fileModel.Namespace);
    }

    [Fact]
    public void UsingStatements_ShouldNotThrow_WhenSerializedToEmptyList()
    {
        // Arrange
        var usingStatements = new List<string>();
        var fileModel = new FileModel(usingStatements);

        // Act
        var deserializedUsingStatements = JsonSerializer.Deserialize<List<string>>(fileModel.UsingStatements);

        // Assert
        Assert.NotNull(deserializedUsingStatements);
        Assert.Empty(deserializedUsingStatements);
    }

    [Fact]
    public void UsingStatements_ShouldSerializeAndDeserialize_WhenPopulated()
    {
        // Arrange
        var usingStatements = new List<string> { "using System;", "using System.Text.Json;" };
        var fileModel = new FileModel(usingStatements);

        // Act
        var deserializedUsingStatements = JsonSerializer.Deserialize<List<string>>(fileModel.UsingStatements);

        // Assert
        Assert.NotNull(deserializedUsingStatements);
        Assert.Equal(usingStatements.Count, deserializedUsingStatements?.Count);
        Assert.Equal(usingStatements, deserializedUsingStatements);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData(null, null)]
    [InlineData("C#", "C#")]
    public void Language_ShouldAllowGetAndSet(string languageInput, string expectedLanguage)
    {
        // Arrange
        var fileModel = new FileModel(new List<string>());

        // Act
        fileModel.Language = languageInput;

        // Assert
        Assert.Equal(expectedLanguage, fileModel.Language);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData(null, null)]
    [InlineData("SomeFilePath.cs", "SomeFilePath.cs")]
    public void FilePath_ShouldAllowGetAndSet(string filePathInput, string expectedFilePath)
    {
        // Arrange
        var fileModel = new FileModel(new List<string>());

        // Act
        fileModel.FilePath = filePathInput;

        // Assert
        Assert.Equal(expectedFilePath, fileModel.FilePath);
    }
}