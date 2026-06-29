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
#   MINI_INCLUDE_DEFAULT_CONFIG=true | false
#   MINI_PROVIDER="openai"
#   MINI_API_BASE="https://example.com/v1"

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
if [ -z "${MINI_CONFIG:-}" ] && [ -n "${MINI_API_BASE:-}" ]; then
  MINI_CONFIG=/attempt/mini-custom-model.yaml
  export MINI_CONFIG
  python3 - "${MINI_CONFIG}" <<'PY'
import json
import os
import sys

def quote(value: str) -> str:
    return json.dumps(value)

with open(sys.argv[1], "w", encoding="utf-8") as output:
    output.write("model:\n")
    output.write(f"  model_name: {quote(os.environ['MINI_MODEL'])}\n")
    output.write("  model_kwargs:\n")
    output.write(f"    api_base: {quote(os.environ['MINI_API_BASE'])}\n")
    output.write(f"    custom_llm_provider: {quote(os.environ.get('MINI_PROVIDER', 'openai'))}\n")
    output.write("  cost_tracking: ignore_errors\n")
PY
fi

if [ -n "${MINI_CONFIG:-}" ]; then
  if [ "${MINI_INCLUDE_DEFAULT_CONFIG:-true}" = "true" ]; then
    CONFIG_ARGS+=(--config mini.yaml)
  fi
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
MINI_INCLUDE_DEFAULT_CONFIG=${MINI_INCLUDE_DEFAULT_CONFIG:-true}
MINI_PROVIDER=${MINI_PROVIDER:-}
MINI_API_BASE=${MINI_API_BASE:-}
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
