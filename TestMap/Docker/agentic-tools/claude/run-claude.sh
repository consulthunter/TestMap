#!/usr/bin/env bash
set -euo pipefail

source /runner/common/agent-runner-lib.sh

require_file /attempt/prompt.md

mkdir -p /attempt

write_metadata_start
capture_git_before
record_version "claude" "claude --version"

CLAUDE_ARGS=()
if [ -n "${CLAUDE_MODEL:-}" ]; then
  CLAUDE_ARGS+=(--model "${CLAUDE_MODEL}")
fi

{
  echo "claude -p <prompt.md> ${CLAUDE_ARGS[*]} --permission-mode ${CLAUDE_PERMISSION_MODE:-dontAsk} --output-format stream-json --max-turns ${CLAUDE_MAX_TURNS:-40} --verbose"
} > /attempt/command.txt

cat > /attempt/runner-env.txt <<EOF
TOOL_ID=claude
CLAUDE_MODEL=${CLAUDE_MODEL:-}
CLAUDE_PERMISSION_MODE=${CLAUDE_PERMISSION_MODE:-dontAsk}
CLAUDE_MAX_TURNS=${CLAUDE_MAX_TURNS:-40}
ANTHROPIC_API_KEY_SET=$([ -n "${ANTHROPIC_API_KEY:-}" ] && echo yes || echo no)
WORKSPACE=/workspace
ATTEMPT=/attempt
EOF

set +e
cd /workspace

claude -p "$(cat /attempt/prompt.md)" \
  "${CLAUDE_ARGS[@]}" \
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
