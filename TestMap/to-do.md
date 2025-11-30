# To Do

List of items / thoughts for implementation and maybe how to go about it.

## List

- Storing data
    - I need some data persistence here. Currently we don't re-use information between runs which could be problematic
      in terms of performance
    - I was thinking using SQLITE for certain information, like solution files, filepaths, project id, etc
        - If we do this, that eliminates needing to rediscover the project each time, we can just load the context from
          the DB
        - Creating the mapping each time is also problematic. We need a way to store the mapping in the DB so that we
          don't have to re-analyze each time
        - Only re-analyze when it's specified
- Need to rethink loop for generation.
    - First check to see if we have a baseline
    - Yes
        - Then generate, replace tests in the codebase, collect the results, store in DB
    - No
        - First extract the information, populate DB, create the initial baseline
        - Then, generate, replace the tests in the codebase, collect the results, store in DB
    - If we continue to generate tests we need access to the previous test run results for the test we are replacing
    - Codebase needs to be reverted back after we are finished, if they want to apply the last test run, they should
      have the option
        - Probably fetch the recent tests and replace, then commit

## Steps

First we need to implement versioning and storing in the DB

When we clone a project, we need the current commit number, to store that as part of the project in the DB

Next, we need to store results in a consistent place, Test Runs for TestMethods, Coverage for
NonTestMethods/NonTestClasses

We need to capture the datetime of the coverage, likely we need to create this in build/test, store that, pass in as
variable, look for coverage files in the results, with the matching datetime

Methods, Classes need unique GUIDs, after a test run we need to map the results/coverage and store in the DB

Analyzing should really only happen once, unless we need to update the DB because the codebase changed

If we are on a new commit hash, we can present them the option to update the DB, this should be on backlog because this
is a research tool not a full-featured tool

Now, for generation

We should already have everything stored in the DB, so when we generate, we only load the file we are working with

SynxtaxTrees can't be modified if I remember correctly, so basically you need a clone, then use the location information
of the method to properly replace the method, likely splice the file cut out
the original, append the new, append the rest of the original

We can store these generated tests separately from the rest of the data, in it's own table, using the test_method_id, to
link them together, with test_run_id

For storing coverage, we can look at the original test and the related production code, to tie together the coverage to
that

We can also add export functionality that lets people export information, like methods -> TestMethods, Classes ->
nonTestClasses, GeneratedTestMethods performance -> Original Performance etc

## Current progress

Added intial DB Logic and config options

De-coupled coverage and results from analysis

Need to create a unique run id for test run in BuildTest

Then bring in the information and map to the information in the DB, selects and inserts

After this, we should have a DB with everything related together, test results, coverage, test methods -> test classes,
etc

Then, we can work on the generation logic because we'll need to load the DB information, then randomly select some
testmethods to generate candidate replacements

We'll only select tests we know to have pass that has coverage

We'll have an easy loop of generate, test, load results, repair, test, atc.

Question, do we replace a single test at a time, do we generate a single test at a time, do we batch it?

Regardless we'll want some way of applying the changes or using the best tests from a test run, likely since we're
storing them with performance data we just look at the best code coverage achieved

Maybe a flag to apply best tests on the generatetests options, we'll only apply the tests if they matched or
outperformed existing tests

We may also want to add something for classes and methods that are not tested or we do not find any for them, may need
to update the model a bit for this then

### Plan

1. ~~Fix data model like the Tars one~~
    - files, imports, classes, properties, methods, etc
2. ~~Update analyze like Tars with out DB logic~~
    - due to model changes
3. Create the Insertion logic
    - ~~insert the project info -> package info -> file info -> class info -> (method info, property info, import
      info)~~
    - Need to update insertion to make sure we don't get duplicate data, currently it keeps adding, not sure why
    - ~~Need to create dedicated class for updated the invocation sourceId, where we find the declaration for a method
      if possible~~
        - Not all invocations will have a source method
    - Add insertion for test results
4. Create the load Logic
    - Should load into existing objects, should maybe include a nullable DBID field for quick lookups
5. Update BuildTest
    - ~~Needs runtime for the test run and a unique ID for that run~~
    - ~~TestRun and Coverage also need the relevant information~~
    - May need to add a script for generation, that only tests the solution that contains the project that contains the
      test
6. Create the Generation portion
    - Test framework needs to be determined as well
    - Load relevant info, files, packages, classes, methods, if it already exists, else should quit
    - Replace or append a single test per run
        - Option to add batching later
    - Analyze the performance of the tests, make sure no breaking changes
    - Should also capture generation time
    - How to check performance, if a new test look at the class level, if a replacement, look at previous test
      performance
    - Create a flag to save the tests and commit to the codebase, make sure upstream is unset
        - If the test is new and it passes and improves the coverage, apply
        - If the test replaces an old test, it passes, and improves the coverage, apply
7. Create the export logic
    - maybe some precreated view so that we can just select, load, export
    - JSON, CSV, others
8. Create new Jupyter notebook to explore the output




