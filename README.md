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
- Git
  - [Instructions](https://git-scm.com/downloads/win)
- Windows 11 (theoretically this could work from Linux)


## Installation

First, ```git clone https://github.com/consulthunter/TestMap```

Next, edit the ```collection-config.json``` located under ```/TestMap/TestMap/Config/```

The JSON config file will have filepaths that need to be updated for your filesystem.

After you've updated the config file, do:

- ```dotnet clean TestMap.sln```
- ```dotnet restore TestMap.sln```
- ```dotnet build TestMap.sln```

Now run the example,

```dotnet run --project .\TestMap\TestMap.csproj collect --config F:\Projects\TestMap\TestMap\Config\collection-config.json```

## How To Use

## Motivation

## How It Works

## Data Availability

## Acknoledgements