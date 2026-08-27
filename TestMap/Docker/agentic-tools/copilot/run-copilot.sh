#!/usr/bin/env bash
# copilot/run-copilot.sh

set -euo pipefail

source /runner/common/agent-runner-lib.sh

require_file /attempt/prompt.md

mkdir -p /attempt

# Optional:
#   COPILOT_HOME=/auth/copilot
#   COPILOT_ALLOW_MODE=allow-all | yolo | none
#   COPILOT_MODEL="gpt-5.3-codex" | "claude-sonnet-4.6" | etc.
#   COPILOT_EXTRA_ARGS="..."
#
# Bring-your-own-key (Copilot talks to the configured endpoint instead of GitHub-hosted models):
#   COPILOT_PROVIDER_BASE_URL="http://host.docker.internal:11434"
#   COPILOT_PROVIDER_TYPE=openai | azure | anthropic   (default openai)
#   COPILOT_PROVIDER_API_KEY="..."                     (omit for unauthenticated local servers)
# BYOK requires a model: Copilot has no default for a provider it does not host. The BYOK model
# must support tool calling and streaming, and GitHub auth is still needed to start the CLI.
export COPILOT_HOME="${COPILOT_HOME:-/tmp/testmap-copilot-home}"
mkdir -p "$COPILOT_HOME"
export COPILOT_OTEL_FILE_EXPORTER_PATH="${COPILOT_OTEL_FILE_EXPORTER_PATH:-/attempt/copilot-otel.jsonl}"

if [ -z "${COPILOT_GITHUB_TOKEN:-}" ] && [ -n "${GITHUB_COPILOT_TOKEN:-}" ]; then
  export COPILOT_GITHUB_TOKEN="${GITHUB_COPILOT_TOKEN}"
fi

write_metadata_start
capture_git_before
record_version "copilot" "copilot --version"

cat > /attempt/runner-env.txt <<EOF
TOOL_ID=github-copilot-cli
COPILOT_HOME=${COPILOT_HOME}
COPILOT_ALLOW_MODE=${COPILOT_ALLOW_MODE:-allow-all}
COPILOT_MODEL=${COPILOT_MODEL:-}
COPILOT_OTEL_FILE_EXPORTER_PATH=${COPILOT_OTEL_FILE_EXPORTER_PATH}
COPILOT_PROVIDER_BASE_URL=${COPILOT_PROVIDER_BASE_URL:-}
COPILOT_PROVIDER_TYPE=${COPILOT_PROVIDER_TYPE:-}
COPILOT_PROVIDER_API_KEY_SET=$([ -n "${COPILOT_PROVIDER_API_KEY:-}" ] && echo yes || echo no)
GITHUB_COPILOT_TOKEN_SET=$([ -n "${GITHUB_COPILOT_TOKEN:-}" ] && echo yes || echo no)
COPILOT_GITHUB_TOKEN_SET=$([ -n "${COPILOT_GITHUB_TOKEN:-}" ] && echo yes || echo no)
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

# Pass the model explicitly. --model outranks COPILOT_MODEL, the settings file and the
# CLI default, so the attempt is pinned to one model and /attempt/command.txt records
# which one. An unset model leaves Copilot on its own default, which can change between
# releases and would make attempts within a sweep incomparable.
MODEL_ARGS=()
if [ -n "${COPILOT_MODEL:-}" ]; then
  MODEL_ARGS+=(--model "${COPILOT_MODEL}")
elif [ -n "${COPILOT_PROVIDER_BASE_URL:-}" ]; then
  # Fail fast rather than letting the CLI error out mid-attempt: there is no default
  # model for a provider Copilot does not host.
  echo "COPILOT_PROVIDER_BASE_URL is set but COPILOT_MODEL is empty; BYOK requires a model." >&2
  write_metadata_end 2
  exit 2
else
  echo "COPILOT_MODEL is not set; falling back to the Copilot CLI default model." >&2
fi

EXTRA_ARGS=()
if [ -n "${COPILOT_EXTRA_ARGS:-}" ]; then
  # Intentional splitting for experiment-controlled arguments.
  # Do not set COPILOT_EXTRA_ARGS from untrusted input.
  # shellcheck disable=SC2206
  EXTRA_ARGS=(${COPILOT_EXTRA_ARGS})
fi

{
  echo "copilot --prompt <prompt.md> --output-format json --no-color ${ALLOW_ARGS[*]} ${MODEL_ARGS[*]} ${EXTRA_ARGS[*]}"
} > /attempt/command.txt

set +e

copilot \
  --prompt "$(cat /attempt/prompt.md)" \
  --output-format json \
  --no-color \
  "${ALLOW_ARGS[@]}" \
  "${MODEL_ARGS[@]}" \
  "${EXTRA_ARGS[@]}" \
  > /attempt/copilot.events.jsonl \
  2> /attempt/copilot.stderr.log

EXIT_CODE=$?

set -e

capture_git_after
write_metadata_end "$EXIT_CODE"

exit "$EXIT_CODE"
