#!/usr/bin/env bash
set -euo pipefail

source /runner/common/agent-runner-lib.sh

require_file /attempt/prompt.md

write_metadata_start
capture_git_before
record_version "claude" "claude --version"

set +e
cd /workspace

claude -p "$(cat /attempt/prompt.md)" \
  --permission-mode "${CLAUDE_PERMISSION_MODE:-dontAsk}" \
  --output-format stream-json \
  --max-turns "${CLAUDE_MAX_TURNS:-40}" \
  --debug-file /attempt/claude-debug.log \
  --verbose \
  > /attempt/claude.events.jsonl \
  2> /attempt/claude.stderr.log

EXIT_CODE=$?
set -e

capture_git_after
write_metadata_end "$EXIT_CODE"

exit "$EXIT_CODE"