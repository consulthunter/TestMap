# How To Use

TestMap is can be used to collect tests from C# repositories.

## Generate Configuration File

Within the ```/TestMap/TestMap/``` directory run
```sh
./TestMap.exe setup
```
This will generate the file ```./Config/default-config.json``` with default values. This will hold the configuration for running the rest of the project.

## Collect Repositories

Under ```/TestMap/Scripts/``` there is a python script that can be used to gather a list of repositories.

The list of repositories should be a text file including the full URL to each repository, for example, ```https://github.com/consulthunter/TestMap```. Put each distinct repository on a new line.

Place the file with the list under the ```/TestMap/TestMap/Data/``` directory.

Now modify the ```/TestMap/TestMap/Config/default-config.json```

- Edit ```FilePaths```
  - Change the ```TargetFilePath``` to the fullpath of the file containing your list of repositories.

### Specify Frameworks

By default, we have defined some attributes across three popular testing frameworks in the ```default-config.json```

Current targets:
- MSTest
- NUnit
- xUnit

You should define the frameworks you want to target under the ```Frameworks``` in the ```default-config.json```

First define the name of the framework as it would appear in the ```usings statments``` such as ```using Xunit;```

Next define the attribute you want to look at, for ```xUnit``` this could be ```[Fact]```, ```[Theory]```, etc.

The framework and its attributes should look something like this:

```json
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

You can modify this list to add or remove frameworks.

Likewise, you can modify the attributes from the framework. 

### Run Collect

Assuming that you have completed the installation from the [README.md](../README.md) and you have defined the list of repositories and testing frameworks in the ```default-config.json```, you can now run the program:

```sh
dotnet run --project ./TestMap/TestMap.csproj collect-tests --config ./TestMap/Config/default-config.json
```
```sh
./TestMap.exe collect-tests --config ./TestMap/Config/default-config.json
```

## Generating Tests
Once the repositories are collected and the tests are extracted from their respective programs, we can generate our own tests to compare.

### Configure AI Model

Within the generated ```/Config/default-config.json``` modify the respective ```Generation``` section:

```Provider``` - This is the provider of the model. Supported providers include:
- openai - models such as GPT
- ollama - locally hosted models
- google - models such as gemini
- amazon - used for antrophic models and others hosted through amazon bedrock
- custom - used for any other custom hosted models

```Model``` - This is the name of the model using

```MaxRetries``` - This is the number of retry requests the program will send to the model before abandoning request

__NOTE__: Each Provider requires a different section for parameters to be added below in the configuration file. Each section will be input in the following section:
```json
"PROVIDER-SECTION-TITLE": {
    "PARAMETER": "PARAMETER-VALUE"
    ...
  },
```

| Provider       | Section Title | Required Parameter | Description                                        |
| -------------- | ------------- | ------------------ | -------------------------------------------------- |
| ```openai```   | OpenAI        | ApiKey             | OpenAI API key                                     |
|                |               | OrgId              | OpenAI organization id                             |
| ```amazon```   | Amazon        | AwsAccessKey       | AWS Access Key ID                                  |
|                |               | AwsSecretKey       | AWS Secret Key ID                                  |
|                |               | AwsRegion          | Region to connect to. See connected regions below. |
| ```google```   | Google        | ApiKey             | Google Gemini API key                              |
| ```ollama```   | Ollama        | Endpoint           | Enpoint to the hosted service                      |
| ```custom```   | Custom        | ApiKey             | Key to use the API                                 |
|                |               | OrgId              | Organization ID                                    |
|                |               | Endpoint           | Endpoint to the service                            |


<details>
  <summary>Supported AWS Regions</summary>

*   us-east-1
*   us-east-2
*   us-west-1
*   us-west-2
*   ca-central-1
*   mx-central-1
</details>

### Run Generate

Once the model parameters are correctly set the following command can be executed to generate and analyze the tests.

```sh
./TestMap.exe generate-tests --config ./TestMap/Config/default-config.json
```

[OUTPUT STUFF]