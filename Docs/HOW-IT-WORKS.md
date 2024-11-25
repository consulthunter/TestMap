# How It Works

TestMap uses the Roslyn API and Microsoft CodeAnalysis to find C# tests within repositories.

After finding the tests, TestMap collects them into CSV files for future use and analysis.

## Collect

The ```collect``` command is the only current command for TestMap.

This starts collecting the tests from repositories.

For each repository we:
- Clone the repo
- Find solutions (.sln)
  - For each solution find projects (.csproj) in the solution
    - For each project load the project's compilation and syntax trees (.cs)

### TestMethods

Test methods are found using the CSharpCompilation and SemanticModel.

This occurs in the ```AnalyzeProjectService```

In each project, we look at the project's compilation and every syntax tree (.cs)

For each syntax tree, we first look for class declarations then for each class, we look for method declarations.

When we find a method declaration, we look at any method attributes. Method attributes are listed
above the method in the ```[Attribute_Here]``` brackets. Test method attributes can come in different forms depending on the test
framework. ```xUnit``` uses several but ```[Fact]``` is the standard attribute for marking a test.

If we found attributes that match those defined for testing frameworks in the configuration file, we say that
this is a test method declaration.

When we find a test method declaration, we look for method invocations in the test method. Our reasoning is that
methods used in the test method are likely to be the method-under-test.

Once we gather method used in the test method, we use the SemanticModel to get the SymbolInfo for that method. The symbol info
will contain the definition of the method used if the method used is defined somewhere in the compilation's syntax trees.

Finally, we use this information with other contextual information to populate TestMethodRecords and write them to a CSV file.

### TestClasses

TestClasses are found using project references and filepaths.

Basically, we find test classes assuming there is a 1-to-1 mapping between test classes and source code classes.

For example, for every Example.cs there is a ExampleTest.cs but this is often not the case.

To find these cases, we load each project and their references into the ```ProjectModel```.

When we analyze a project in ```AnalyzeProjectService```, we look for class declarations. 

When we find a class declaration, we look through projects in the ProjectModel to find projects that have the same filepath as
references within the current project we are analyzing.

After we find projects that match, we trim the filepath of the current syntax tree (the test class) and search the syntax trees
of the other projects, looking for filepaths that match the current document.