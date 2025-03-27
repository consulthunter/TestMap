
# TestMap
[![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.14262975.svg)](https://doi.org/10.5281/zenodo.14262975)
![License](https://img.shields.io/badge/License-MIT-yellow.svg)
![Language](https://img.shields.io/badge/Language-CSharp-blue.svg)



TestMap is a tool for gathering software tests from C# repositories from GitHub and other Git based developer platforms.

It collects software tests using SyntaxTrees and the Roslyn API.
## Dependencies

- Windows 11
- .NET SDK 9.0 or greater
  - [Instructions](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
  - (winget is suggested)
      - ```winget install Microsoft.DotNet.SDK.9```

  __Note: dotnet needs to be on PATH. Make sure that ```dotnet --version``` works through the CLI__

- Git
  - [Instructions](https://git-scm.com/downloads/win)
- Docker Desktop
  - [Instructions](https://docs.docker.com/get-started/introduction/get-docker-desktop/)


## Installation

Create a directory, named ```Projects```.
- ```mkdir Projects```

Navigate to the ```Projects``` directory.
- ```cd .\Projects\```

Now clone TestMap
- ```git clone https://github.com/consulthunter/TestMap```

Download the latest release into the Release directory.

Change into the Release directory:
- ```cd Release```

Generate the config:
- ```.\TestMap.exe generate-config --path D:\Projects\TestMap\TestMap\Config\new-config.json --base-path D:\Projects\TestMap```
- _Note_: Replace ```D:\Projects``` with your directory prefix.

Next try running the project:
- ```.\TestMap.exe collect --config D:\Projects\TestMap\TestMap\Config\new-config.json```
- _Note_: Update the path to the generated config.

If this didn't work, try building and publishing yourself. See [Building.](#building-and-publishing)

## Building And Publishing

More details on building and publishing the tool for use [here.](./Docs/BUILDING-PUBLISHING.md)

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