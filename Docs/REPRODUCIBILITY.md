# Reproducibility

To reproduce the datasets listed in [Data Availability](./DATA-AVAILABILITY.md)

Follow the installation instructions from the [README](../README.md)

Assuming you have successfully run the example.

Do the following:

- Copy the ```repositories_urls.txt``` from ```/TestMap/Scripts/collectRepos/``` to ```/TestMap/TestMap/Data/```
- Modify the ```collection-config.json``` located under ```.\TestMap\TestMap\Config\```:
  - Modify ```FilePaths```
    - ```TargetFilePath``` to: ```<Your_DIR_Prefix>\\Projects\\TestMap\\TestMap\\Data\\repositories_urls.txt```
- Start the run: 
  - ```dotnet run --project .\TestMap\TestMap.csproj collect --config .\TestMap\Config\collection-config.json```
- After all the projects have been analyzed, use the Jupyter notebook ```preprocess.ipynb``` to load and preprocess all the project CSVs
  - You should only need to modify the following line ```directory_path = "D:/Projects/TestMap/TestMap/Output/"```
  - Point this to the root directory of the output folder on your machine, as specified in the ```collection-config.json```

## Considerations

TestMap will do projects concurrently. Occasionally, a project will hang and fail to release. As such, you may need to stop the program
and modify the ```repositories_urls.txt```, removing the projects that have already completed. Then, start the program again starting at the
project that it stopped on. Additionally, there will be projects that fail to complete because the project is too large to analyze, i.e., several GBs.

The repositories we had problems analyzed are listed below and can be removed from the list (these would be beneficial to at least try analyzing):
- https://github.com/vivet/GoogleApi
- https://github.com/Azure/azure-sdk-for-net
- https://github.com/aws/aws-sdk-net
- https://github.com/realm/realm-dotnet
- https://github.com/QutEcoacoustics/audio-analysis
- https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet
- https://github.com/googleapis/google-cloud-dotnet
- https://github.com/lindexi/lindexi_gd
- https://github.com/vchelaru/FlatRedBall
- https://github.com/X-Sharp/XSharpPublic
- https://github.com/microsoft/PowerApps-Samples
- https://github.com/Samsung/TizenFX
- https://github.com/phuocle/Dynamics-Crm-DevKit
- https://github.com/WeihanLi/SamplesInPractice
- https://github.com/microsoft/Dynamics365-Apps-Samples
- https://github.com/dotnet/maui
- https://github.com/abpframework/abp-samples
- https://github.com/colinin/abp-next-admin
- https://github.com/aws/porting-assistant-dotnet-client
- https://github.com/microsoftgraph/msgraph-cli
- https://github.com/masastack/MASA.Framework
- https://github.com/CodeMazeBlog/CodeMazeGuides

Some of the repositories may eventually be made private, as such, full reproducibility may be impossible. 

We also should have specified the exact commit the repo was at when cloned. 

In future iterations, we will specify the commit and try to find a more static source of repositories.


## Recommendations

Use a machine with a large amount of RAM (64 GB or more) and a decent processor (8+ cores) if re-creating the datasets. 