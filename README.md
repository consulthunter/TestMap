# TestMap

TestMap is a C# analysis and test-engineering tool for collecting project data, mapping execution results back onto code, and using that data to support test generation workflows.

At a high level, TestMap can:
- clone and inspect C# repositories
- build a persisted model of solutions, projects, files, objects, members, and relationships
- collect test execution results such as coverage, mutation results, code metrics, and test smells
- use those results to select generation targets and run AI-assisted test generation pipelines
- run experiment workflows that compare providers, strategies, and outcomes

## What It Does

The project is organized around a pipeline model. Each run mode assembles a sequence of steps for a project, such as cloning, loading the database, extracting code information, running analysis, collecting results, and generating tests.

Current top-level run modes include:
- `setup`
- `check-projects`
- `static-analysis`
- `collect-tests`
- `generate-tests`
- `experiment`

## Core Capabilities

These are the foundation of the project and the part I would describe as the main platform rather than experimental research work:

- Pipeline-based execution for multi-project runs
- Static analysis of C# solutions and projects
- Persistence of code structure and analysis results into SQLite
- Collection and mapping of:
  - coverage results
  - mutation testing results
  - code metrics
  - test smells
- Test execution and validation workflows
- AI-provider abstraction for test generation backends

## Experimental Features

Several of the most interesting parts of TestMap are still best described as experimental. They are implemented and usable, but they rely on heuristics, evolving workflows, and still need more validation.

### AI-driven test generation

TestMap includes a decomposed generation pipeline rather than a single prompt. It can generate scenarios, method names, arrange/input/action/assertion plans, and final test code, then run repair loops when generation fails to compile or does not improve outcomes.

This includes:
- provider-agnostic generation across multiple AI backends
- multi-step prompt orchestration
- compile-repair and behavior-repair flows
- action executors and generation approaches that are still evolving

### Test bootstrap for projects with no tests

One of the clearest experimental features is bootstrap mode for projects that do not already contain test infrastructure.

TestMap can detect when a project has no discovered tests, create an initial test project, scaffold starter test files, and inject that temporary test project into the in-memory project context so generation can proceed. This is useful, but it is still an early capability and should be treated as exploratory.

### Test improvement workflows

TestMap is not limited to generating brand new tests. It also includes logic aimed at improving existing tests and prioritizing methods or tests that look weak.

This area is experimental because it depends on combining multiple signals, such as:
- weak mutation outcomes
- coverage gaps
- test smells
- weak assertion quality
- flakiness-related signals

### Metric-driven target selection

The project includes a metric-driven candidate selection strategy that tries to prioritize generation work based on expected improvement to a chosen metric.

The strategy can score methods using signals such as:
- mutation weakness
- line or branch coverage opportunity
- coverage gaps
- lack of direct test signals
- estimated feasibility and cost

This is an important research feature, but it is still heuristic-driven and not yet something I would present as a fully validated decision system.

### Risk-weighted target selection

TestMap can rank candidate methods using weighted risk factors like complexity, coverage gaps, mutation survival, call graph signals, churn, and missing tests.

This is valuable for prioritization, but the quality of the output depends heavily on configuration, weighting, and the quality of the underlying collected data.

### Flaky test detection

The project includes a flaky-test detection model built from:
- historical execution data
- rerun behavior
- duration variance
- failure signatures
- environment-related signals

This is a strong candidate for documentation as experimental because it is inherently probabilistic and still needs more validation against real projects.

### Provider-comparison experiments

The `experiment` mode is explicitly research-oriented. It runs candidate methods through different providers and strategies such as single-pass, repeated attempts, and repair loops, then persists attempts and exports results for comparison.

This is one of the project’s most distinctive capabilities, but it should be documented as an experiment system rather than a stable product surface.

## Still In Development

These are the areas that appear to still be under active development or hardening:

- Better automated tests and broader regression coverage
- More robust validation of mapping accuracy between external tool outputs and persisted code entities
- Better performance and scaling on large projects
- Better isolation of generation and experiment attempts
- Better handling of unusual solution layouts and test project structures
- More reliable workflows for projects with partial test infrastructure
- Clearer product boundaries between collection, generation, improvement, and experiment modes

## Current Limitations

At the moment, the project should be approached as an actively evolving system rather than a polished production tool.

Important limitations include:
- some features are heuristic-heavy and may not generalize cleanly across all C# repositories
- several higher-level capabilities are better described as research workflows than stable product features
- the project still needs stronger automated validation, especially around advanced generation and evaluation paths
- result quality depends on the quality of external tools, repository structure, and AI provider behavior

## Planned Direction

The likely next steps for the project are:

- harden the core collection and mapping pipeline
- improve measurement and validation of experimental features
- make bootstrap and test-improvement workflows more reliable
- improve target selection strategies through better data and clearer evaluation
- strengthen experiment reproducibility and result reporting
- refine the documentation so users can distinguish stable capabilities from exploratory ones

## Status Summary

The simplest way to describe TestMap today is:

- the core pipeline for collecting and persisting project and test-analysis data is the platform
- AI-assisted test generation, bootstrap, test improvement, metric-driven selection, flaky-test detection, and provider-comparison experiments are the most important experimental layers
- the project is promising, but still in an active development and validation phase
