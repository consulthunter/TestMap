#!/usr/bin/env bash
# copilot/run-copilot.sh

set -euo pipefail

source /runner/common/agent-runner-lib.sh

require_file /attempt/prompt.md

mkdir -p /attempt

# Optional:
#   COPILOT_HOME=/auth/copilot
#   COPILOT_ALLOW_MODE=allow-all | yolo | none
#   COPILOT_EXTRA_ARGS="..."
export COPILOT_HOME="${COPILOT_HOME:-/tmp/testmap-copilot-home}"
mkdir -p "$COPILOT_HOME"

write_metadata_start
capture_git_before
record_version "copilot" "copilot --version"

cat > /attempt/runner-env.txt <<EOF
TOOL_ID=github-copilot-cli
COPILOT_HOME=${COPILOT_HOME}
COPILOT_ALLOW_MODE=${COPILOT_ALLOW_MODE:-allow-all}
WORKSPACE=/workspace
ATTEMPT=/attempt
EOF

cd /workspace

ALLOW_ARGS=()
case "${COPILOT_ALLOW_MODE:-allow-all}" in
  allow-all)
    ALLOW_ARGS+=(--allow-all)
    ;;
  yolo)
    ALLOW_ARGS+=(--yolo)
    ;;
  none)
    ;;
  *)
    echo "Unknown COPILOT_ALLOW_MODE: ${COPILOT_ALLOW_MODE}" >&2
    write_metadata_end 2
    exit 2
    ;;
esac

EXTRA_ARGS=()
if [ -n "${COPILOT_EXTRA_ARGS:-}" ]; then
  # Intentional splitting for experiment-controlled arguments.
  # Do not set COPILOT_EXTRA_ARGS from untrusted input.
  # shellcheck disable=SC2206
  EXTRA_ARGS=(${COPILOT_EXTRA_ARGS})
fi

{
  echo "copilot --prompt <prompt.md> ${ALLOW_ARGS[*]} ${EXTRA_ARGS[*]}"
} > /attempt/command.txt

set +e

copilot \
  --prompt "$(cat /attempt/prompt.md)" \
  "${ALLOW_ARGS[@]}" \
  "${EXTRA_ARGS[@]}" \
  > /attempt/copilot.stdout.log \
  2> /attempt/copilot.stderr.log

EXIT_CODE=$?

set -e

capture_git_after
write_metadata_end "$EXIT_CODE"

exit "$EXIT_CODE"