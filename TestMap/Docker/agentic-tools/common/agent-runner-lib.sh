#!/usr/bin/env bash
# common/agent-runner-lib.sh

set -euo pipefail

require_file() {
  local path="$1"
  if [ ! -f "$path" ]; then
    echo "Required file missing: $path" >&2
    exit 2
  fi
}

record_version() {
  local name="$1"
  local command="$2"
  bash -lc "$command" > "/attempt/${name}-version.txt" 2>&1 || true
}

configure_git_workspace() {
  git config --global --add safe.directory /workspace >/dev/null 2>&1 || true
}

# Scaffolding written into the workspace by TestMap (.testmap) and by the agent CLIs
# themselves. None of it is an edit to the project under test, so it must not reach
# patch.diff or changed-files.txt: without this an attempt that did no work still reports a
# multi-kilobyte patch. Kept in sync with ExcludedRootDirectories in
# TestMap/Services/AgentTools/AgentToolChangedPathFilter.cs.
SCAFFOLDING_PATHSPECS=(
  '.'
  ':(exclude).testmap'
  ':(exclude).aider'
  ':(exclude).claude'
  ':(exclude).codex'
  ':(exclude).copilot'
  ':(exclude).gemini'
  ':(exclude).openhands'
  ':(exclude).mini-swe-agent'
  ':(exclude).swe-agent'
)

list_untracked_files() {
  git -C /workspace ls-files --others --exclude-standard -- "${SCAFFOLDING_PATHSPECS[@]}" || true
}

# Writes the whole working tree (tracked edits plus non-ignored untracked files) as a tree
# object through a throwaway index, so the workspace's own index and HEAD are untouched.
snapshot_workspace_tree() {
  local index_file
  index_file="$(mktemp /tmp/testmap-snapshot-index.XXXXXX)" || return 1
  rm -f "$index_file"
  if GIT_INDEX_FILE="$index_file" git -C /workspace read-tree HEAD >/dev/null 2>&1 \
    && GIT_INDEX_FILE="$index_file" git -C /workspace add -A -- "${SCAFFOLDING_PATHSPECS[@]}" >/dev/null 2>&1; then
    GIT_INDEX_FILE="$index_file" git -C /workspace write-tree
    local status=$?
    rm -f "$index_file"
    return $status
  fi
  rm -f "$index_file"
  return 1
}

capture_git_before() {
  configure_git_workspace
  git -C /workspace rev-parse HEAD > /attempt/base-commit.txt || true
  git -C /workspace status --short > /attempt/git-before.txt || true
  # The tool's change is measured against this snapshot rather than HEAD, so anything that
  # already differed from HEAD before the tool ran (line-ending conversion, leftovers) is
  # never attributed to the attempt. git-before.txt keeps the pre-existing state visible.
  snapshot_workspace_tree > /attempt/base-tree.txt 2>/dev/null || rm -f /attempt/base-tree.txt
}

capture_git_after() {
  configure_git_workspace

  # Diff against the commit the workspace started on, not the working tree.
  # Agents frequently stage or commit their work, and a bare "git diff" reports
  # only unstaged changes, so a committed result reads as though the agent
  # produced nothing. Diffing from the base commit captures committed, staged
  # and unstaged changes alike; untracked files are appended separately below.
  local base_commit=""
  if [ -s /attempt/base-commit.txt ]; then
    base_commit="$(tr -d '[:space:]' < /attempt/base-commit.txt)"
  fi
  if [ -z "$base_commit" ] || ! git -C /workspace cat-file -e "${base_commit}^{commit}" 2>/dev/null; then
    base_commit=""
  fi

  git -C /workspace status --short > /attempt/git-after.txt || true
  git -C /workspace rev-parse HEAD > /attempt/head-commit.txt 2>/dev/null || true

  local base_tree="" after_tree=""
  if [ -s /attempt/base-tree.txt ]; then
    base_tree="$(tr -d '[:space:]' < /attempt/base-tree.txt)"
  fi
  if [ -n "$base_tree" ] && git -C /workspace cat-file -e "${base_tree}^{tree}" 2>/dev/null; then
    after_tree="$(snapshot_workspace_tree 2>/dev/null)" || after_tree=""
  fi
  if [ -n "$after_tree" ]; then
    # Tree-to-tree diff: covers committed, staged, unstaged and new untracked files alike.
    git -C /workspace diff --binary "$base_tree" "$after_tree" -- "${SCAFFOLDING_PATHSPECS[@]}" \
      > /attempt/patch.diff || true
    git -C /workspace diff --name-only "$base_tree" "$after_tree" -- "${SCAFFOLDING_PATHSPECS[@]}" \
      | awk 'NF' | sort -u > /attempt/changed-files.txt || true
    return 0
  fi

  # Fallback when no snapshot could be taken: diff against the base commit.
  if [ -n "$base_commit" ]; then
    git -C /workspace diff --binary "$base_commit" -- "${SCAFFOLDING_PATHSPECS[@]}" \
      > /attempt/patch.diff || true
  else
    git -C /workspace diff --binary HEAD -- "${SCAFFOLDING_PATHSPECS[@]}" > /attempt/patch.diff \
      || git -C /workspace diff --binary -- "${SCAFFOLDING_PATHSPECS[@]}" > /attempt/patch.diff \
      || true
  fi

  {
    if [ -n "$base_commit" ]; then
      git -C /workspace diff --name-only "$base_commit" -- "${SCAFFOLDING_PATHSPECS[@]}" || true
    else
      git -C /workspace diff --name-only HEAD -- "${SCAFFOLDING_PATHSPECS[@]}" \
        || git -C /workspace diff --name-only -- "${SCAFFOLDING_PATHSPECS[@]}" || true
    fi
    list_untracked_files
  } | awk 'NF' | sort -u > /attempt/changed-files.txt

  while IFS= read -r file_path; do
    [ -n "$file_path" ] || continue
    [ -f "/workspace/$file_path" ] || continue
    git -C /workspace diff --binary --no-index -- /dev/null "$file_path" >> /attempt/patch.diff 2>/dev/null || true
  done < <(list_untracked_files)
}

write_metadata_start() {
  date -u +"%Y-%m-%dT%H:%M:%SZ" > /attempt/start-time.txt
}

write_metadata_end() {
  local exit_code="$1"
  echo "$exit_code" > /attempt/exit-code.txt
  date -u +"%Y-%m-%dT%H:%M:%SZ" > /attempt/end-time.txt
}
