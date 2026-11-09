<!--
Sync Impact Report
- Version change: constitution template -> 1.0.0
- Initial principles:
  - Scientific Reproducibility and Immutable Provenance
  - Explicit Units, Scope, and Pairing
  - Fair and Isolated Lane Comparison
  - Honest Outcomes and Auditable Failures
  - Verification Proportional to Scientific Risk
- Added sections:
  - Experimental Data Contract
  - Development and Review Workflow
- Templates updated:
  - updated: .specify/templates/plan-template.md
  - updated: .specify/templates/spec-template.md
  - updated: .specify/templates/tasks-template.md
- Runtime guidance updated:
  - updated: README.md
- Repository tracking updated:
  - updated: .gitignore to version the constitution and synchronized templates
- Deferred items: none
-->
# TestMap Constitution

## Core Principles

### I. Scientific Reproducibility and Immutable Provenance
TestMap is a scientific Mining Software Repositories and AI-generated-test evaluation instrument.
Every reported observation MUST be traceable to the repository identity, exact source commit,
configuration snapshot, experiment and attempt identifiers, and the model or tool identity available
at execution time. A requested repository revision MUST be resolved and verified before source
extraction or evaluation; TestMap MUST NOT silently substitute a default branch, newer commit, stale
workspace, or previously persisted analysis. Randomized selection MUST record its seed and sampling
frame. Generated datasets MUST retain enough provenance to reproduce exclusions, joins, and outcome
classifications. These requirements make independent reproduction and later audit possible.

### II. Explicit Units, Scope, and Pairing
Every metric, outcome, and dataset MUST declare its unit of analysis and measurement scope. Attempt,
generated-test, test-result, candidate, project, repository, and experiment grains MUST NOT be mixed
without an explicit aggregation rule. Method coverage MUST be compared with method coverage from the
same target identity and compatible runs; project- or repository-level mutation scores MUST remain
labelled at that scope even when mutants map to methods. Baseline and post-attempt values used for a
delta MUST be paired measurements from compatible contexts. Missing or unresolved evidence MUST be
represented as unavailable, never converted to zero. Thresholds, attribution policies, and outcome
rules MUST be named, versioned, and shared across lanes when they support a shared comparison. This
prevents plausible-looking measurements from answering a different question than the study claims.

### III. Fair and Isolated Lane Comparison
The generic single-shot LLM lane and agentic lane MUST be compared on the same immutable candidate
cohort, source revision, baseline evidence, and shared outcome definitions. Lane-specific evidence,
such as Roslyn diagnostics or agent logs, MUST be analyzed separately and MUST NOT confer an
unavailable advantage in a shared comparison. Every attempt MUST start from the declared clean base
revision, and rollback MUST restore that revision rather than an incidental current `HEAD`. When an
agent creates multiple tests, validation and coverage or mutation impact MUST remain attributed to
the overall attempt unless an independently measured per-test effect exists; child rows MUST state
that attribution. Ordering, retries, budgets, exclusions, and unavailable tools MUST be recorded.
These rules protect the comparison from contamination and asymmetric interpretation.

### IV. Honest Outcomes and Auditable Failures
Validation success and positive metric impact MUST remain distinct. A passing low-impact test is a
validated success but MUST NOT be called positive impact. Compilation failures, test failures,
timeouts, crashes, no-change attempts, skipped work, constraint violations, unavailable tools, and
unevaluable measurements MUST remain explicit outcomes rather than being dropped or merged into
success rates. Missingness MUST include its reason and source where known. Logs, prompts, responses,
patches, changed-file information, and diagnostic evidence needed for audit or qualitative coding
MUST be retained subject to secret redaction and documented storage limits. Required collection or
export failures MUST fail visibly instead of producing silently empty artifacts. Failure evidence is
a first-class research result, not merely debugging residue.

### V. Verification Proportional to Scientific Risk
Changes that can alter candidate eligibility, source identity, baseline construction, lane
isolation, measurement, classification, persistence, export grain, or statistical interpretation
MUST include focused regression tests and an end-to-end or integration check at the affected
boundary. Schema changes MUST include migrations, an updated schema snapshot, and export-contract
tests. Analysis changes MUST include fixture-based dataset tests and notebook or report smoke checks
where applicable. The research-prototype status permits iteration, but it does not permit silently
changing the meaning of existing data: incompatible semantics MUST receive a schema or policy
version and a documented migration or explicit non-compatibility decision. Implementation MUST favor
clear, existing project patterns over unnecessary abstraction so the scientific behavior remains
reviewable.

## Experimental Data Contract

- A target is identified by normalized repository identity plus an exact commit. Target manifests,
  resolved revisions, cohort records, resume keys, result rows, and analysis keys MUST agree.
- Evaluation cohorts MUST be created from a documented sampling frame and explicit eligibility rule.
  For paired baseline comparisons, each candidate MUST have a grounded source-to-test mapping and a
  valid baseline test outcome before randomization.
- Canonical result files MUST declare a schema version and row kind. Derived datasets MUST preserve
  the canonical attempt identifier and document every aggregation, deduplication, and exclusion.
- Coverage, mutation, test smells, code metrics, costs, and durations MUST carry measurement scope,
  availability, and attribution metadata sufficient to prevent invalid joins or comparisons.
- Attempt-weighted, candidate-weighted, and repository-weighted summaries MUST remain separate.
  Statistical tests MUST match the design, including pairing, clustering, repeated attempts, and
  unequal repository participation. Estimates MUST report uncertainty and sample size alongside
  significance tests.
- Pilot and publication datasets MUST include integrity audits for revision identity, cohort
  membership, duplicate keys, missing outcomes, unpaired metrics, unavailable usage, and generated
  test links. Audit failures affecting validity MUST block confirmatory analysis.

## Development and Review Workflow

1. A specification that affects experimental behavior MUST state the research purpose, unit of
   analysis, eligibility and exclusion rules, measurement scope, baseline pairing, missingness
   semantics, provenance requirements, and lane-comparison consequences.
2. The implementation plan MUST pass the Constitution Check before design and again after the data
   model and contracts are defined. Any exception MUST be listed in Complexity Tracking with its
   scientific consequence and mitigation.
3. Tests MUST be written at the lowest useful level and extended across persistence, CLI, workspace,
   or analysis boundaries when the change can affect reported evidence. Tiny checked-in fixtures are
   preferred for deterministic integration and notebook validation.
4. Code review MUST inspect behavioral and statistical validity, not only compilation and style.
   Reviewers MUST verify row grain, identity, pairing, missingness, outcome semantics, isolation, and
   backward-compatibility claims relevant to the change.
5. Before a pilot, the complete configured workflow MUST run on a small multi-repository fixture or
   pilot set. The resulting database, canonical CSVs, manifests, audits, and notebooks MUST be
   inspected together before scaling the evaluation.

## Governance

This constitution governs TestMap's research behavior and supersedes conflicting informal practices.
An amendment requires a documented rationale, an impact assessment covering code, schemas, datasets,
analysis, and existing experiments, and updates to affected Spec Kit templates or runtime guidance.
Constitution versions follow semantic versioning: MAJOR for incompatible principle or governance
changes, MINOR for new principles or materially expanded obligations, and PATCH for non-semantic
clarifications. Every feature plan and review MUST record constitution compliance. A temporary
deviation requires an explicit written exception, affected outputs, mitigation, expiration condition,
and approval by the research owner. Research claims MUST cite the constitution and policy versions
under which their data was produced when later amendments could change interpretation.

**Version**: 1.0.0 | **Ratified**: 2026-07-16 | **Last Amended**: 2026-07-16
