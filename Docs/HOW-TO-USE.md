# How To Use

TestMap is can be used to collect tests from C# repositories.

## Collect Repos

Under ```/TestMap/Scripts/``` we have a python script that can be used to gather a list of repositories.

The list of repositories should be the full URL to the repository, for example, ```https://github.com/consulthunter/TestMap```

Put the file with this under the ```/TestMap/TestMap/Data/``` directory.

Now modify the ```/TestMap/TestMap/Config/collection-config.json```

- Edit ```FilePaths```
  - Change the ```TargetFilePath``` to the fullpath of the file containing your list of repositories.

### Specify Frameworks

By default, we have defined some attributes across three popular testing frameworks in the ```collection-config.json```

Current targets:
- MSTest
- NUnit
- xUnit

You should define the frameworks you want to target under the ```Frameworks``` in the ```collection-config.json```

First define the name of the framework as it would appear in the ```usings statments``` such as ```using Xunit;```

Next define the attribute you want to look at, for ```xUnit``` this could be ```[Fact]```, ```[Theory]```, etc.

The framework and its attributes should look something like this:

```
  "Frameworks": {
    "NUnit": [
      "Test",
      "Theory"
    ],
    "xUnit": [
      "Fact",
      "Theory"
    ],
    "MSTest": [
      "TestMethod",
      "DataSource"
    ],
    "Microsoft.VisualStudio.TestTools.UnitTesting": [
      "TestMethod",
      "DataSource"
    ]
  },
```

You can add to this list if you want to add more frameworks. Or you can remove frameworks.

Likewise, you can add attributes from the framework. Or you can remove attributes from the framework.

### Run Collect

Assuming that you have completed the installation from the [README.md](../README.md).

And you have defined the list of repositories and testing frameworks in the ```collection-config.json```

You can now run the program:

```dotnet run --project .\TestMap\TestMap.csproj collect --config .\TestMap\Config\collection-config.json```

