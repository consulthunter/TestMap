#!/usr/bin/env bash
set -euo pipefail

source /runner/common/agent-runner-lib.sh

require_file /attempt/prompt.md

mkdir -p /attempt

write_metadata_start
capture_git_before
record_version "codex" "codex --version"

# ============================================================
# Codex auth policy: API-key login only
# ============================================================
#
# This runner intentionally does NOT use:
#   - mounted ~/.codex/auth.json
#   - copied auth.json from the host
#   - interactive browser/device login
#
# Authentication must be provided through an environment variable,
# usually from docker run --env-file .agent-secrets.env.
#
# Required:
#   OPENAI_API_KEY
#
# Optional:
#   OPENAI_ORG_ID
#   OPENAI_PROJECT_ID
#
# The script performs:
#   printf '%s' "$OPENAI_API_KEY" | codex login --with-api-key
#
# This creates Codex's runtime auth state inside the container only.
# ============================================================

if [ -z "${OPENAI_API_KEY:-}" ]; then
  echo "Missing OPENAI_API_KEY." >&2
  echo "This Codex runner requires API-key login via: codex login --with-api-key" >&2
  write_metadata_end 2
  exit 2
fi

# Use an isolated Codex home so auth/config are created inside the container
# and do not depend on any host-mounted Codex state.
export CODEX_HOME="${CODEX_HOME:-/tmp/codex-home}"
mkdir -p "$CODEX_HOME"

# If Codex also checks XDG config locations, isolate those too.
export XDG_CONFIG_HOME="${XDG_CONFIG_HOME:-/tmp/codex-xdg-config}"
export XDG_DATA_HOME="${XDG_DATA_HOME:-/tmp/codex-xdg-data}"
export XDG_STATE_HOME="${XDG_STATE_HOME:-/tmp/codex-xdg-state}"
mkdir -p "$XDG_CONFIG_HOME" "$XDG_DATA_HOME" "$XDG_STATE_HOME"

# Record auth shape only. Never write secret values to /attempt.
{
  echo "AUTH_MODE=codex-login-with-api-key"
  echo "OPENAI_API_KEY_SET=yes"
  echo "OPENAI_ORG_ID_SET=$([ -n "${OPENAI_ORG_ID:-}" ] && echo yes || echo no)"
  echo "OPENAI_PROJECT_ID_SET=$([ -n "${OPENAI_PROJECT_ID:-}" ] && echo yes || echo no)"
  echo "CODEX_HOME=${CODEX_HOME}"
  echo "XDG_CONFIG_HOME=${XDG_CONFIG_HOME}"
  echo "XDG_DATA_HOME=${XDG_DATA_HOME}"
  echo "XDG_STATE_HOME=${XDG_STATE_HOME}"
} > /attempt/auth-mode.txt

# Guardrail: do not allow accidental host auth.json usage.
# We do not fail if Codex creates auth.json inside CODEX_HOME during login.
if [ -f "${HOME}/.codex/auth.json" ]; then
  {
    echo "Warning: ${HOME}/.codex/auth.json exists."
    echo "This runner does not intentionally use mounted/copied host auth.json."
    echo "Codex login will be performed with OPENAI_API_KEY into isolated CODEX_HOME=${CODEX_HOME}."
  } > /attempt/auth-warning.txt
fi

# ============================================================
# Login to Codex using API key
# ============================================================

set +e

# Do not use `printenv OPENAI_API_KEY` here because some environments can
# add formatting surprises. printf avoids a trailing newline.
printf '%s' "$OPENAI_API_KEY" \
  | codex login --with-api-key \
  > /attempt/codex-login.stdout.log \
  2> /attempt/codex-login.stderr.log

LOGIN_EXIT_CODE=$?

set -e

if [ "$LOGIN_EXIT_CODE" -ne 0 ]; then
  echo "codex login --with-api-key failed with exit code ${LOGIN_EXIT_CODE}." >&2
  echo "See /attempt/codex-login.stderr.log for details." >&2
  capture_git_after
  write_metadata_end "$LOGIN_EXIT_CODE"
  exit "$LOGIN_EXIT_CODE"
fi

# Optional status check. If this fails, do not leak secrets; just fail early.
set +e

codex login status \
  > /attempt/codex-login-status.stdout.log \
  2> /attempt/codex-login-status.stderr.log

STATUS_EXIT_CODE=$?

set -e

if [ "$STATUS_EXIT_CODE" -ne 0 ]; then
  echo "codex login status failed with exit code ${STATUS_EXIT_CODE}." >&2
  echo "See /attempt/codex-login-status.stderr.log for details." >&2
  capture_git_after
  write_metadata_end "$STATUS_EXIT_CODE"
  exit "$STATUS_EXIT_CODE"
fi

# ============================================================
# Run Codex
# ============================================================

set +e
cd /workspace

codex exec - \
  --cd /workspace \
  --ignore-user-config \
  --sandbox danger-full-access \
  --json \
  --output-last-message /attempt/final-message.md \
  -m "${CODEX_MODEL:-gpt-5.3-codex}" \
  -c approval_policy="never" \
  -c model_reasoning_effort="${CODEX_REASONING_EFFORT:-high}" \
  -c model_reasoning_summary="${CODEX_REASONING_SUMMARY:-auto}" \
  -c sandbox_workspace_write.network_access="${CODEX_NETWORK_ACCESS:-true}" \
  < /attempt/prompt.md \
  > /attempt/codex.events.jsonl \
  2> /attempt/codex.stderr.log

EXIT_CODE=$?
set -e

capture_git_after
write_metadata_end "$EXIT_CODE"

exit "$EXIT_CODE"