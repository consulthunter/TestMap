# ✅ To Do: Implementation Plan for Persistent, Versioned Test Analysis Tool

This document outlines current thoughts, implementation steps, and features to build a version-aware, baseline-driven test/coverage analysis tool using SQLite and Docker.

---

## 🗃️ Data Persistence

### Goals:
- Improve performance across multiple runs.
- Avoid unnecessary re-analysis and rebuilding of mappings.

### Plan:
- **Use SQLite** to store persistent project metadata:
    - Solution files, file paths, project ID, method/class GUIDs.
- **Cache mapping** information (method → test method) to avoid regenerating each time.
- **Store the commit hash** as part of each project record:
    - Helps identify if code has changed.
    - Add a `last_analyzed_commit` field to the project table.

---

## 🔁 Test Generation Workflow

### Workflow:
1. **Check if baseline exists**:
    - ✅ Yes → Generate new tests → Replace in codebase → Run & analyze → Store results.
    - ❌ No → Extract baseline data → Save to DB → Generate and analyze like above.

### Notes:
- Must **revert test changes** in the codebase unless explicitly committed.
- New test results must be linked to original method/test entries for tracking improvement or regression.

---

## 📦 Coverage & Result Storage

### Implementation Steps:
- Store results in **dedicated DB tables**:
    - `test_runs`, `coverage_entries`, `test_methods`, `non_test_methods`, etc.
- Capture **datetime** and **commit hash** for each test/coverage run.
- Each run should include:
    - Full set of test results (pass/fail, duration, errors).
    - Coverage entries mapped to methods/classes.
- Track **changes between runs**:
    - Number of test failures fixed.
    - Coverage % differences.
    - Potential improvement or degradation indicators.

---

## 🧠 Analysis Strategy

### Key Ideas:
- Analyze codebase only once per commit unless forced.
- If on a **new commit**, prompt user to re-analyze or reuse previous mapping.
- Baseline runs should be **read-only and locked** to prevent overwrites.

---

## 🧪 Test Generation & Code Manipulation

### Approach:
- Use file path + span info from the DB to locate test methods in source files.
- Since `SyntaxTree` is immutable, use string manipulation (splice) to:
    - Cut original method.
    - Inject generated version.
    - Append the remainder.
- Store generated tests in a separate DB table:
    - Linked via `test_method_id`, `test_run_id`.

---

## 📤 Export / Reporting

### Planned Exports:
- Mapping of Production Methods → Test Methods.
- Coverage and performance comparison (Original vs. Generated).
- Export formats: CSV, JSON, possibly HTML for reporting.

---

## 🧩 Additional Ideas & Enhancements

- ✅ **Store last analyzed commit hash** in DB to avoid re-analyzing unnecessarily.
- ✅ **Track metrics** on coverage/test result drift between runs.
- ✅ **Support codebase reversion**:
    - Options: `git stash`, branch checkout, or patch files.
    - Reapply or discard generated tests safely.
