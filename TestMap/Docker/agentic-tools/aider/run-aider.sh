#!/usr/bin/env bash
# aider/run-aider.sh

set -euo pipefail

source /runner/common/agent-runner-lib.sh

require_file /attempt/prompt.md

mkdir -p /attempt

# Optional:
#   AIDER_MODEL="sonnet" | "openai/gpt-4.1" | "anthropic/claude-sonnet-4-5" | etc.
#   AIDER_YES=true | false
#   AIDER_AUTO_COMMITS=true | false
#   AIDER_EXTRA_ARGS="..."
#
# Expected auth env depends on model provider:
#   OPENAI_API_KEY
#   ANTHROPIC_API_KEY
#   GEMINI_API_KEY
#   etc.

write_metadata_start
capture_git_before
record_version "aider" "aider --version"

cd /workspace

AIDER_ARGS=()

if [ -n "${AIDER_MODEL:-}" ]; then
  AIDER_ARGS+=(--model "${AIDER_MODEL}")
fi

# For scripted evaluation, avoid interactive confirmations.
if [ "${AIDER_YES:-true}" = "true" ]; then
  AIDER_ARGS+=(--yes-always)
fi

# I would usually disable auto-commits for the experiment because TestMap
# captures the diff itself. Enable only if you intentionally want Aider commits.
if [ "${AIDER_AUTO_COMMITS:-false}" = "false" ]; then
  AIDER_ARGS+=(--no-auto-commits)
fi

# Keep output/logging predictable.
AIDER_ARGS+=(--no-pretty)
AIDER_ARGS+=(--exit)
AIDER_ARGS+=(--no-gitignore)

EXTRA_ARGS=()
if [ -n "${AIDER_EXTRA_ARGS:-}" ]; then
  # Intentional splitting for experiment-controlled arguments.
  # Do not set AIDER_EXTRA_ARGS from untrusted input.
  # shellcheck disable=SC2206
  EXTRA_ARGS=(${AIDER_EXTRA_ARGS})
fi

{
  echo "aider ${AIDER_ARGS[*]} ${EXTRA_ARGS[*]} --message-file /attempt/prompt.md"
} > /attempt/command.txt

cat > /attempt/runner-env.txt <<EOF
TOOL_ID=aider
AIDER_MODEL=${AIDER_MODEL:-}
AIDER_YES=${AIDER_YES:-true}
AIDER_AUTO_COMMITS=${AIDER_AUTO_COMMITS:-false}
WORKSPACE=/workspace
ATTEMPT=/attempt
EOF

set +e

aider \
  "${AIDER_ARGS[@]}" \
  "${EXTRA_ARGS[@]}" \
  --message-file /attempt/prompt.md \
  > /attempt/aider.stdout.log \
  2> /attempt/aider.stderr.log

EXIT_CODE=$?

set -e

capture_git_after
write_metadata_end "$EXIT_CODE"

exit "$EXIT_CODE"
