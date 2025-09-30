using System.Collections.Generic;
using System.Text.Json;
using JetBrains.Annotations;
using TestMap.Models.Code;
using Xunit;

namespace TestMap.Tests.UnitTests.Models.Code;

[TestSubject(typeof(ClassModel))]
public class ClassModelTest
{
    [Fact]
    public void Constructor_ShouldInitializeProperties_WithDefaultValues()
    {
        // Act
        var model = new ClassModel();

        // Assert
        Assert.Equal(0, model.FileId);
        Assert.Empty(model.Guid);
        Assert.Empty(model.Name);
        Assert.Empty(model.Visibility);
        Assert.Equal("null", model.Modifiers);
        Assert.Equal("null", model.Attributes);
        Assert.Empty(model.FullString);
        Assert.Empty(model.DocString);
        Assert.False(model.IsTestClass);
        Assert.Empty(model.TestingFramework);
        Assert.NotNull(model.Location);
        Assert.Equal(0, model.Location.StartLineNumber);
        Assert.Equal(0, model.Location.EndLineNumber);
        Assert.Equal(0, model.Location.BodyStartPosition);
        Assert.Equal(0, model.Location.BodyEndPosition);
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties_WithProvidedValues()
    {
        // Arrange
        var testAttributes = new List<string> { "Serializable", "Deprecated" };
        var testModifiers = new List<string> { "public", "sealed" };
        var location = new Location(1, 2, 3, 4);

        // Act
        var model = new ClassModel(
            fileId: 102,
            guid: "1234-abcd",
            name: "TestClass",
            visibility: "public",
            attributes: testAttributes,
            modifiers: testModifiers,
            fullString: "class TestClass {}",
            docString: "Documentation for TestClass",
            isTestClass: true,
            testingFramework: "xUnit",
            location: location
        );

        // Assert
        Assert.Equal(102, model.FileId);
        Assert.Equal("1234-abcd", model.Guid);
        Assert.Equal("TestClass", model.Name);
        Assert.Equal("public", model.Visibility);
        Assert.Equal(JsonSerializer.Serialize(testModifiers), model.Modifiers);
        Assert.Equal(JsonSerializer.Serialize(testAttributes), model.Attributes);
        Assert.Equal("class TestClass {}", model.FullString);
        Assert.Equal("Documentation for TestClass", model.DocString);
        Assert.True(model.IsTestClass);
        Assert.Equal("xUnit", model.TestingFramework);
        Assert.Same(location, model.Location);
    }

    [Fact]
    public void Id_Property_ShouldSetAndGetValue()
    {
        // Arrange
        var model = new ClassModel();

        // Act
        model.Id = 444;

        // Assert
        Assert.Equal(444, model.Id);
    }

    [Fact]
    public void FileId_Property_ShouldSetAndGetValue()
    {
        // Arrange
        var model = new ClassModel();

        // Act
        model.FileId = 555;

        // Assert
        Assert.Equal(555, model.FileId);
    }

    [Fact]
    public void Guid_Property_ShouldSetAndGetValue()
    {
        // Arrange
        var model = new ClassModel();

        // Act
        model.Guid = "unique-guid";

        // Assert
        Assert.Equal("unique-guid", model.Guid);
    }

    [Fact]
    public void Name_Property_ShouldSetAndGetValue()
    {
        // Arrange
        var model = new ClassModel();

        // Act
        model.Name = "MyClass";

        // Assert
        Assert.Equal("MyClass", model.Name);
    }

    [Fact]
    public void Visibility_Property_ShouldSetAndGetValue()
    {
        // Arrange
        var model = new ClassModel();

        // Act
        model.Visibility = "private";

        // Assert
        Assert.Equal("private", model.Visibility);
    }

    [Fact]
    public void Modifiers_Property_ShouldSerializeListCorrectly()
    {
        // Arrange
        var model = new ClassModel();
        var modifiers = new List<string> { "public", "abstract" };

        // Act
        model.Modifiers = JsonSerializer.Serialize(modifiers);

        // Assert
        Assert.Equal(JsonSerializer.Serialize(modifiers), model.Modifiers);
    }

    [Fact]
    public void Attributes_Property_ShouldSerializeListCorrectly()
    {
        // Arrange
        var model = new ClassModel();
        var attributes = new List<string> { "Serializable", "Generated" };

        // Act
        model.Attributes = JsonSerializer.Serialize(attributes);

        // Assert
        Assert.Equal(JsonSerializer.Serialize(attributes), model.Attributes);
    }

    [Fact]
    public void FullString_Property_ShouldSetAndGetValue()
    {
        // Arrange
        var model = new ClassModel();

        // Act
        model.FullString = "class MyClass {}";

        // Assert
        Assert.Equal("class MyClass {}", model.FullString);
    }

    [Fact]
    public void DocString_Property_ShouldSetAndGetValue()
    {
        // Arrange
        var model = new ClassModel();

        // Act
        model.DocString = "This is a test class";

        // Assert
        Assert.Equal("This is a test class", model.DocString);
    }

    [Fact]
    public void IsTestClass_Property_ShouldSetAndGetValue()
    {
        // Arrange
        var model = new ClassModel();

        // Act
        model.IsTestClass = true;

        // Assert
        Assert.True(model.IsTestClass);
    }

    [Fact]
    public void TestingFramework_Property_ShouldSetAndGetValue()
    {
        // Arrange
        var model = new ClassModel();

        // Act
        model.TestingFramework = "NUnit";

        // Assert
        Assert.Equal("NUnit", model.TestingFramework);
    }

    [Fact]
    public void Location_Property_ShouldSetAndGetValue()
    {
        // Arrange
        var model = new ClassModel();
        var location = new Location(10, 20, 30, 40);

        // Act
        model.Location = location;

        // Assert
        Assert.Equal(location, model.Location);
    }
}