#!/usr/bin/env bash
# gemini/run-gemini.sh

set -euo pipefail

source /runner/common/agent-runner-lib.sh

require_file /attempt/prompt.md

mkdir -p /attempt

# Optional:
#   GEMINI_MODEL="gemini-2.5-pro" | "gemini-2.5-flash" | etc.
#   GEMINI_OUTPUT_FORMAT=stream-json | json | text
#   GEMINI_APPROVAL_MODE=yolo | auto_edit | default
#   GEMINI_SANDBOX=true | false
#   GEMINI_EXTRA_ARGS="..."
#
# Expected auth env for headless Docker runs:
#   GEMINI_API_KEY or GOOGLE_API_KEY
#   GOOGLE_GENAI_USE_VERTEXAI=true and GOOGLE_CLOUD_PROJECT for Vertex AI

export GEMINI_CLI_TRUST_WORKSPACE="${GEMINI_CLI_TRUST_WORKSPACE:-true}"
export TERM="${TERM:-xterm-256color}"
export COLORTERM="${COLORTERM:-truecolor}"

write_metadata_start
capture_git_before
record_version "gemini" "gemini --version"

cd /workspace

GEMINI_ARGS=()

if [ -n "${GEMINI_MODEL:-}" ]; then
  GEMINI_ARGS+=(--model "${GEMINI_MODEL}")
fi

GEMINI_OUTPUT_FORMAT="${GEMINI_OUTPUT_FORMAT:-stream-json}"
GEMINI_ARGS+=(--output-format "${GEMINI_OUTPUT_FORMAT}")

if [ "${GEMINI_SANDBOX:-false}" = "true" ]; then
  GEMINI_ARGS+=(--sandbox)
fi

if [ "${GEMINI_APPROVAL_MODE:-yolo}" = "yolo" ]; then
  GEMINI_ARGS+=(--approval-mode yolo)
elif [ -n "${GEMINI_APPROVAL_MODE:-}" ]; then
  GEMINI_ARGS+=(--approval-mode "${GEMINI_APPROVAL_MODE}")
fi

EXTRA_ARGS=()
if [ -n "${GEMINI_EXTRA_ARGS:-}" ]; then
  # Intentional splitting for experiment-controlled arguments.
  # Do not set GEMINI_EXTRA_ARGS from untrusted input.
  # shellcheck disable=SC2206
  EXTRA_ARGS=(${GEMINI_EXTRA_ARGS})
fi

{
  echo "gemini ${GEMINI_ARGS[*]} ${EXTRA_ARGS[*]} --prompt <prompt.md>"
} > /attempt/command.txt

cat > /attempt/runner-env.txt <<EOF
TOOL_ID=gemini
GEMINI_MODEL=${GEMINI_MODEL:-}
GEMINI_OUTPUT_FORMAT=${GEMINI_OUTPUT_FORMAT}
GEMINI_APPROVAL_MODE=${GEMINI_APPROVAL_MODE:-yolo}
GEMINI_SANDBOX=${GEMINI_SANDBOX:-false}
GEMINI_API_KEY_SET=$([ -n "${GEMINI_API_KEY:-}" ] && echo yes || echo no)
GOOGLE_API_KEY_SET=$([ -n "${GOOGLE_API_KEY:-}" ] && echo yes || echo no)
GOOGLE_GENAI_USE_VERTEXAI=${GOOGLE_GENAI_USE_VERTEXAI:-}
GOOGLE_CLOUD_PROJECT_SET=$([ -n "${GOOGLE_CLOUD_PROJECT:-}" ] && echo yes || echo no)
GEMINI_CLI_TRUST_WORKSPACE=${GEMINI_CLI_TRUST_WORKSPACE}
TERM=${TERM}
COLORTERM=${COLORTERM}
WORKSPACE=/workspace
ATTEMPT=/attempt
EOF

set +e

GEMINI_STDOUT_PATH=/attempt/gemini.stdout.log
if [ "${GEMINI_OUTPUT_FORMAT}" = "stream-json" ]; then
  GEMINI_STDOUT_PATH=/attempt/gemini.events.jsonl
elif [ "${GEMINI_OUTPUT_FORMAT}" = "json" ]; then
  GEMINI_STDOUT_PATH=/attempt/gemini.json
fi

gemini \
  "${GEMINI_ARGS[@]}" \
  "${EXTRA_ARGS[@]}" \
  --prompt "$(cat /attempt/prompt.md)" \
  > "${GEMINI_STDOUT_PATH}" \
  2> /attempt/gemini.stderr.log

EXIT_CODE=$?

set -e

capture_git_after
write_metadata_end "$EXIT_CODE"

exit "$EXIT_CODE"
