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

capture_git_before() {
  git -C /workspace rev-parse HEAD > /attempt/base-commit.txt || true
  git -C /workspace status --short > /attempt/git-before.txt || true
}

capture_git_after() {
  git -C /workspace status --short > /attempt/git-after.txt || true
  git -C /workspace diff --binary > /attempt/patch.diff || true
  git -C /workspace diff --name-only > /attempt/changed-files.txt || true
}

write_metadata_start() {
  date -u +"%Y-%m-%dT%H:%M:%SZ" > /attempt/start-time.txt
}

write_metadata_end() {
  local exit_code="$1"
  echo "$exit_code" > /attempt/exit-code.txt
  date -u +"%Y-%m-%dT%H:%M:%SZ" > /attempt/end-time.txt
}