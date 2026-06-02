#!/usr/bin/env bash
# mini-swe-agent/run-mini-swe-agent.sh

set -euo pipefail

source /runner/common/agent-runner-lib.sh

require_file /attempt/prompt.md

mkdir -p /attempt

export MSWEA_CONFIGURED="${MSWEA_CONFIGURED:-true}"

# Optional:
#   MINI_ALLOW_MODE=yolo | none
#   MINI_EXTRA_ARGS="..."
#   MINI_MODEL="anthropic/claude-sonnet-4-5"
#   MINI_CONFIG=/attempt/mini-config.yaml

write_metadata_start
capture_git_before
record_version "mini-swe-agent" "mini --version || mini --help"

cd /workspace

ALLOW_ARGS=()
case "${MINI_ALLOW_MODE:-yolo}" in
  yolo)
    ALLOW_ARGS+=(--yolo)
    ;;
  none)
    ;;
  *)
    echo "Unknown MINI_ALLOW_MODE: ${MINI_ALLOW_MODE}" >&2
    write_metadata_end 2
    exit 2
    ;;
esac

MODEL_ARGS=()
if [ -n "${MINI_MODEL:-}" ]; then
  export MSWEA_MODEL_NAME="${MINI_MODEL}"
  MODEL_ARGS+=(--model "${MINI_MODEL}")
fi

CONFIG_ARGS=()
if [ -n "${MINI_CONFIG:-}" ]; then
  CONFIG_ARGS+=(--config "${MINI_CONFIG}")
fi

EXTRA_ARGS=()
if [ -n "${MINI_EXTRA_ARGS:-}" ]; then
  # Intentional splitting for experiment-controlled arguments.
  # Do not set MINI_EXTRA_ARGS from untrusted input.
  # shellcheck disable=SC2206
  EXTRA_ARGS=(${MINI_EXTRA_ARGS})
fi

{
  echo "mini ${ALLOW_ARGS[*]} ${MODEL_ARGS[*]} ${CONFIG_ARGS[*]} ${EXTRA_ARGS[*]} --exit-immediately --output /attempt/mini-swe-agent-trajectory.json --task <prompt.md>"
} > /attempt/command.txt

cat > /attempt/runner-env.txt <<EOF
TOOL_ID=mini-swe-agent
MINI_ALLOW_MODE=${MINI_ALLOW_MODE:-yolo}
MINI_MODEL=${MINI_MODEL:-}
MINI_CONFIG=${MINI_CONFIG:-}
WORKSPACE=/workspace
ATTEMPT=/attempt
EOF

set +e

mini \
  "${ALLOW_ARGS[@]}" \
  "${MODEL_ARGS[@]}" \
  "${CONFIG_ARGS[@]}" \
  "${EXTRA_ARGS[@]}" \
  --exit-immediately \
  --output /attempt/mini-swe-agent-trajectory.json \
  --task "$(cat /attempt/prompt.md)" \
  > /attempt/mini-swe-agent.stdout.log \
  2> /attempt/mini-swe-agent.stderr.log

EXIT_CODE=$?

set -e

capture_git_after
write_metadata_end "$EXIT_CODE"

exit "$EXIT_CODE"
