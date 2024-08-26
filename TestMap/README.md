# TestMap

This is a work-in-progress.

Currently, the idea is to automatically clone and build C# projects from Github.

Since I use the built-in terminal for Windows (i.e. Powershell), this is a Windows-only project.

I attempted using some other Nuget packages to avoid PowerShell usage, but it did not work.

So far, I am getting the results I expect to see in the dataset
I delimit each entry in the csv with single-quotes `'`. Additionally, for the source code
entries, I replace all newlines with `<<NEWLINE>>`


Problems:
- Finding the correct assemblies to import. FIXED
  - Assemblies can be in several different locations depending on package and framework
- Not all assemblies have MetaDate references. FIXED
- winget search Microsoft.DotNet.SDK
- Add project reference assemblies
- Need to open solution find all of the csharp files for the solution or project
  - parse them DONE
  - pass them to compilation DONE
- Projects can reference other project outside of the current project leading to unknown types.
  - I need a build solution project service that builds all of the solutions DONE
  - After getting all of the solutions and projects/project references, do build project service DONE
  - I need to load all of the projects and project information DONE
  - Finally iterate through each project and build the compilation etc. DONE

Current Plan:
- Abstract ProjectModel DONE
- Project Model will have a list of solutions DONE
- A new service to build all of the solutions DONE
- Revise build service to do all of the assemblies, syntax trees, etc. DONE
  - Then do a single project compilation and analysis removing the compilation at the end DONE
- Do not add assemblies from project references that have AssemblyInfo or AssemblyAttributes

Steps:
- Need to add logging DONE
- Change to using string manipulation instead of URI when cloning DONE
- Need to add a list format (DONE)
- Need to add Tests (STARTED)
- Need to add error handling if the project is not cloned. 
- Need to scan the project's `.csproj` file for the SDK reference
  - After retrieving the SDK version, try to match with results from `winget`
  - Install the right SDK with `winget`
  - Add error handling for `winget`
- Restore dependencies for the project file with `dotnet restore` DONE
  - add error handling
- Build the project with `dotnet build` DONE
  - add error handling
- After successful building of the project, extract the assemblies (DONE)
- Go through the syntax tree and try to create a semantic model, getting the info (DONE)
- Add info to a csv and voila DONE

Future:
- Run data collection script get valid list of repos.
- Conduct holdout for 100 projects, plus 3 MVPs
- Upload datasets to HuggingFace
- Create a new dataset containing source code summaries using CodeTrans [model](https://huggingface.co/SEBIS/code_trans_t5_large_source_code_summarization_csharp_multitask)
- Create a new RAG pipeline with retrieve and re-rank