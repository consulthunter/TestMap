# How It Works

TestMap uses the Roslyn API and Microsoft CodeAnalysis to find C# tests within repositories.

After finding the tests, TestMap collects them into an SQLITE database.

## Collect-Tests

The ```collect-tests``` command is used to collect tests from repositories.

This starts collecting the tests from repositories.

For each repository we:
- Clone the repo
- Find solutions (.sln)
  - For each solution find projects (.csproj) in the solution
    - For each project load the project's compilation and syntax trees (.cs)

- After collecting all the tests, TestMap will use Docker to run the tests.
- After running the tests, TestMap will analyze the results and store them in the SQLITE database.

## Generate-Tests

The ```generate-tests``` command is used to generate tests and integrate them into the target project.

TestMap uses the SQLITE database to retrieve contextual information about the project's existing tests.

Then, collects source code methods with an existing test that has less than 100% coverage (Line Rate).

TestMap uses SemanticKernel to query an LLM of your choice to generate a new test, integrate it, run it, and collect the results.

If the generated test fails, TestMap will retry the test up to your specified number of times.

If the generated test passes, TestMap will assess the coverage to determine improvement.

If the test fails to improve coverage, it will not be integrated into the project.
