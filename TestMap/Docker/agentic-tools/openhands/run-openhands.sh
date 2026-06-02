#!/usr/bin/env bash
# openhands/run-openhands.sh

set -euo pipefail

source /runner/common/agent-runner-lib.sh

require_file /attempt/prompt.md

mkdir -p /attempt

# Optional:
#   OPENHANDS_APPROVAL_MODE=always-approve | llm-approve | none
#   OPENHANDS_EXTRA_ARGS="..."
#   LLM_MODEL="anthropic/claude-sonnet-4-5"
#   LLM_API_KEY="..."
#   LLM_BASE_URL="..."
#
# Notes:
#   --override-with-envs allows OpenHands to use LLM_* env vars.
#   Use only inside an isolated Docker runner when using always-approve.

write_metadata_start
capture_git_before
record_version "openhands" "openhands --version || openhands --help"

cd /workspace

APPROVAL_ARGS=()
case "${OPENHANDS_APPROVAL_MODE:-always-approve}" in
  always-approve)
    APPROVAL_ARGS+=(--always-approve)
    ;;
  llm-approve)
    APPROVAL_ARGS+=(--llm-approve)
    ;;
  none)
    ;;
  *)
    echo "Unknown OPENHANDS_APPROVAL_MODE: ${OPENHANDS_APPROVAL_MODE}" >&2
    write_metadata_end 2
    exit 2
    ;;
esac

EXTRA_ARGS=()
if [ -n "${OPENHANDS_EXTRA_ARGS:-}" ]; then
  # Intentional splitting for experiment-controlled arguments.
  # Do not set OPENHANDS_EXTRA_ARGS from untrusted input.
  # shellcheck disable=SC2206
  EXTRA_ARGS=(${OPENHANDS_EXTRA_ARGS})
fi

{
  echo "openhands --headless --json --override-with-envs ${APPROVAL_ARGS[*]} ${EXTRA_ARGS[*]} --task <prompt.md>"
} > /attempt/command.txt

cat > /attempt/runner-env.txt <<EOF
TOOL_ID=openhands
OPENHANDS_APPROVAL_MODE=${OPENHANDS_APPROVAL_MODE:-always-approve}
LLM_MODEL=${LLM_MODEL:-}
LLM_BASE_URL=${LLM_BASE_URL:-}
WORKSPACE=/workspace
ATTEMPT=/attempt
EOF

set +e

openhands \
  --headless \
  --json \
  --override-with-envs \
  "${APPROVAL_ARGS[@]}" \
  "${EXTRA_ARGS[@]}" \
  --task "$(cat /attempt/prompt.md)" \
  > /attempt/openhands.events.jsonl \
  2> /attempt/openhands.stderr.log

EXIT_CODE=$?

set -e

capture_git_after
write_metadata_end "$EXIT_CODE"

exit "$EXIT_CODE"