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
#   OpenHands documents --json as JSONL, but some builds can still emit plain
#   text on stdout. Keep raw stdout and only publish valid JSON objects as JSONL.

write_metadata_start
capture_git_before
record_version "openhands" "openhands --version || openhands --help"

cd /workspace

# Keep file-editor temporary files on the workspace filesystem. Moving a file
# from /tmp onto a Docker Desktop bind mount falls back to copy2, whose metadata
# preservation is not supported by the Windows bind-mount filesystem.
export TMPDIR="${OPENHANDS_TMPDIR:-/workspace/.testmap/tmp}"
export SAVE_TRAJECTORY_PATH="${SAVE_TRAJECTORY_PATH:-/attempt/openhands-trajectories}"
export OH_PERSISTENCE_DIR="${OH_PERSISTENCE_DIR:-/attempt/openhands-state}"
export OPENHANDS_SUPPRESS_BANNER="${OPENHANDS_SUPPRESS_BANNER:-1}"
export DISABLE_COLOR="${DISABLE_COLOR:-true}"
export NO_COLOR="${NO_COLOR:-1}"
export TERM="${TERM:-dumb}"
export TTY_COMPATIBLE="${TTY_COMPATIBLE:-0}"
export TTY_INTERACTIVE="${TTY_INTERACTIVE:-0}"
mkdir -p "$TMPDIR"
mkdir -p "$SAVE_TRAJECTORY_PATH"
mkdir -p "$OH_PERSISTENCE_DIR"

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
  echo "stdout=/attempt/openhands.stdout.log"
  echo "jsonl=/attempt/openhands.events.jsonl when stdout contains JSON object lines"
  echo "state=/attempt/openhands-state"
} > /attempt/command.txt

cat > /attempt/runner-env.txt <<EOF
TOOL_ID=openhands
OPENHANDS_APPROVAL_MODE=${OPENHANDS_APPROVAL_MODE:-always-approve}
LLM_MODEL=${LLM_MODEL:-}
LLM_BASE_URL=${LLM_BASE_URL:-}
SAVE_TRAJECTORY_PATH=${SAVE_TRAJECTORY_PATH}
OH_PERSISTENCE_DIR=${OH_PERSISTENCE_DIR}
TMPDIR=${TMPDIR}
OPENHANDS_SUPPRESS_BANNER=${OPENHANDS_SUPPRESS_BANNER}
DISABLE_COLOR=${DISABLE_COLOR}
NO_COLOR=${NO_COLOR}
TERM=${TERM}
TTY_COMPATIBLE=${TTY_COMPATIBLE}
TTY_INTERACTIVE=${TTY_INTERACTIVE}
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
  > /attempt/openhands.stdout.log \
  2> /attempt/openhands.stderr.log

EXIT_CODE=$?

set -e

python3 - /attempt/openhands.stdout.log /attempt/openhands.events.jsonl <<'PY'
import json
import os
import sys

source, target = sys.argv[1], sys.argv[2]
tmp = target + ".tmp"
count = 0

with open(source, "r", encoding="utf-8", errors="replace") as src, \
     open(tmp, "w", encoding="utf-8") as dst:
    for line in src:
        text = line.strip()
        if not text:
            continue
        try:
            value = json.loads(text)
        except json.JSONDecodeError:
            continue
        if isinstance(value, dict):
            dst.write(json.dumps(value, separators=(",", ":")) + "\n")
            count += 1

if count:
    os.replace(tmp, target)
else:
    os.remove(tmp)
PY

capture_git_after
write_metadata_end "$EXIT_CODE"

exit "$EXIT_CODE"
