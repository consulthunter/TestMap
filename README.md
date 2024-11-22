# TestMap

TestMap is a tool for gathering software tests from C# repositories from GitHub and other Git based developer platforms.

It collects software tests using SyntaxTrees and the Roslyn API. Then creates two CSV files for each target C# repository.

CSV files:
- ```test_methods_{random_number}_{Repo}.csv```
    - File containing test methods found in ```.cs``` files in the repo and definitions for methods invoked within the test method.
- ```test_classes_{random_number}_{Repo}.csv```
    - File containing test classes found in ```.cs``` files in the repo and their corresponding source code class.

__Note:__ There may be some overlap between the two files. However, the ```test_methods``` is generally a more complete picture of the tests found in the repo.
```test_methods``` uses a more fine-grained search. ```test_classes``` assumes a 1-to-1 mapping between test code files and source code files, which is typically not the case unless for unit tests.
```test_methods``` captures all of the tests using the frameworks and attributes defined by the user and does NOT assume the 1-to-1 mapping between test code files and source code files.

## Dependencies


- .NET SDK 8.0 or greater
  - [Instructions](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
  - (winget is suggested)
      - ```winget install Microsoft.DotNet.SDK.8```
- Git
  - [Instructions](https://git-scm.com/downloads/win)
- Windows 11 (theoretically this could work from Linux)


## Installation

Create a directory, named ```Projects```.
- ```mkdir Projects```

Navigate to the ```Projects``` directory.
- ```cd .\Projects\```

Now clone TestMap
- ```git clone https://github.com/consulthunter/TestMap```

Next, edit the ```collection-config.json``` located under ```.\TestMap\TestMap\Config\```
- Modify ```FilePaths```
    - ```TargetFilePath``` to: ```<Your__DIR_Prefix>\\Projects\\TestMap\\TestMap\\Date\\example_projec.txt```
    - ```LogsDirPath``` to: ```<Your_DIR_Prefix>\\Projects\\TestMap\\TestMap\\Logs```
    - ```TempDirPath``` to: ```<Your_DIR_Prefix>\\Projects\\TestMap\\Temp```
    - ```OutputDirPath``` to: ```<Your_DIR_Prefix>\\Projects\\TestMap\\TestMap\\Output```
- Modify ```Scripts```
    - ```Delete``` to:  ```<Your_DIR_Prefix>\\Projects\\TestMap\\TestMap\\Scripts\\run_rm.bat```

Navigate to the TestMap root
- ```cd TestMap```

Try restoring the dependencies:
- ```dotnet restore .\TestMap\TestMap.csproj```

Next try building the project:
- ```dotnet build .\TestMap\TestMap.csproj```

If both restoring the dependencies and building the project succeeded without errors:

Run the example,

```dotnet run --project .\TestMap\TestMap.csproj collect --config .\TestMap\Config\collection-config.json```

## How To Use

## Motivation

## How It Works

## Data Availability

## Acknoledgements
