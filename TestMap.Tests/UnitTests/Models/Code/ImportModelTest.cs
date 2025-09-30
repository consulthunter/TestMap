using JetBrains.Annotations;
using TestMap.Models.Code;
using Xunit;

namespace TestMap.Tests.UnitTests.Models.Code;

[TestSubject(typeof(ImportModel))]
public class ImportModelTest
{
    [Fact]
    public void Constructor_ShouldInitializeAllProperties_WithDefaultValues()
    {
        // Act
        var model = new ImportModel();

        // Assert
        Assert.Equal(0, model.FileId);
        Assert.Equal(0, model.Id);
        Assert.Equal("", model.Guid);
        Assert.Equal("", model.ImportName);
        Assert.Equal("", model.ImportPath);
        Assert.Equal("", model.FullString);
        Assert.False(model.IsLocal);
    }

    [Fact]
    public void Constructor_ShouldInitializeAllProperties_WithProvidedValues()
    {
        // Arrange
        int fileId = 123;
        string guid = "test-guid";
        string importName = "Test Import";
        string importPath = "/test/path";
        string fullString = "Test Full String";
        bool isLocal = true;

        // Act
        var model = new ImportModel(fileId, guid, importName, importPath, fullString, isLocal);

        // Assert
        Assert.Equal(fileId, model.FileId);
        Assert.Equal(guid, model.Guid);
        Assert.Equal(importName, model.ImportName);
        Assert.Equal(importPath, model.ImportPath);
        Assert.Equal(fullString, model.FullString);
        Assert.True(model.IsLocal);
    }

    [Fact]
    public void Properties_ShouldAllowGettingAndSettingValues()
    {
        // Arrange
        var model = new ImportModel();

        int fileId = 100;
        int id = 1;
        string guid = "new-guid";
        string importName = "New Import Name";
        string importPath = "/new/import/path";
        string fullString = "New Full String";
        bool isLocal = true;

        // Act
        model.FileId = fileId;
        model.Id = id;
        model.Guid = guid;
        model.ImportName = importName;
        model.ImportPath = importPath;
        model.FullString = fullString;
        model.IsLocal = isLocal;

        // Assert
        Assert.Equal(fileId, model.FileId);
        Assert.Equal(id, model.Id);
        Assert.Equal(guid, model.Guid);
        Assert.Equal(importName, model.ImportName);
        Assert.Equal(importPath, model.ImportPath);
        Assert.Equal(fullString, model.FullString);
        Assert.True(model.IsLocal);
    }

    [Theory]
    [InlineData(0, "guid-1", "Import1", "/path/1", "FullString1", false)]
    [InlineData(100, "guid-2", "Import2", "/path/2", "FullString2", true)]
    [InlineData(-1, "", "", "", "", false)]
    [InlineData(int.MaxValue, "guid-max", "ImportMax", "/path/max", "FullMax", true)]
    public void Constructor_ShouldSetPropertiesCorrectly_WithVariousInputs(
        int fileId,
        string guid,
        string importName,
        string importPath,
        string fullString,
        bool isLocal)
    {
        // Act
        var model = new ImportModel(fileId, guid, importName, importPath, fullString, isLocal);

        // Assert
        Assert.Equal(fileId, model.FileId);
        Assert.Equal(guid, model.Guid);
        Assert.Equal(importName, model.ImportName);
        Assert.Equal(importPath, model.ImportPath);
        Assert.Equal(fullString, model.FullString);
        Assert.Equal(isLocal, model.IsLocal);
    }
}