
# TestMap
![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.14262975.svg)
![License](https://img.shields.io/badge/License-MIT-yellow.svg)
![Language](https://img.shields.io/badge/Language-CSharp-blue.svg)


TestMap is a tool for gathering software tests from C# repositories from GitHub and other Git based developer platforms.

It collects software tests using SyntaxTrees and the Roslyn API. Then creates two CSV files for each target C# repository.

CSV files:
- ```test_methods_{random_number}_{Repo}.csv```
    - File containing test methods found in ```.cs``` files in the repo and definitions for methods invoked within the test method.
- ```test_classes_{random_number}_{Repo}.csv```
    - File containing test classes found in ```.cs``` files in the repo and their corresponding source code class.

__Note:__ There may be some overlap between the two files. However, the ```test_methods``` is generally a more complete picture of the tests found in the repo.
```test_methods``` uses a more fine-grained search. ```test_classes``` assumes a 1-to-1 mapping between test code files and source code files, which is typically not the case unless for unit tests.
```test_methods``` captures all the tests using the frameworks and attributes defined by the user and does NOT assume the 1-to-1 mapping between test code files and source code files.

## Dependencies


- .NET SDK 8.0 or greater
  - [Instructions](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
  - (winget is suggested)
      - ```winget install Microsoft.DotNet.SDK.8```

  __Note: dotnet needs to be on PATH. Make sure that ```dotnet --version``` works through the CLI__

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
    - ```TargetFilePath``` to: ```<Your__DIR_Prefix>\\Projects\\TestMap\\TestMap\\Date\\example_project.txt```
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

If both restoring the dependencies and building the project succeeded without errors, you can now use the tool.

## Example Usage

After completing the installation, try running the example:

```dotnet run --project .\TestMap\TestMap.csproj collect --config .\TestMap\Config\collection-config.json```

## How To Use

More details on how to use this tool is available [here.](./Docs/HOW-TO-USE.md)

## Testing

Unit tests for TestMap are generally run through GitHub Actions.

Integration tests typically need to be done locally.

For details on testing you can find more information [here.](./Docs/TESTING.md)

## Motivation

Detailed motivation for this tool is available [here.](./Docs/MOTIVATION.md)

## How It Works

Technical detail on how the tool works is available [here.](./Docs/HOW-IT-WORKS.md)

## Future Work

We have some ideas for future work located [here.](./Docs/FUTURE-WORK.md)

## Data Availability

Information on datasets created using this tool is located [here.](./Docs/DATA-AVAILABILITY.md)

## Reproducibility

Information on re-creating the datasets listed ih Data Availability is located [here.](./Docs/REPRODUCIBILITY.md)