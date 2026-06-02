FROM testmap-dotnet-agent-base:0.1

ARG CODEX_CLI_VERSION=latest

# Install Codex CLI.
# For reproducible evaluation runs, replace "latest" with a pinned version
# after the pilot stabilizes.
RUN npm install -g @openai/codex@${CODEX_CLI_VERSION}

# Shared runner helper and Codex-specific execution script.
COPY common/agent-runner-lib.sh /runner/common/agent-runner-lib.sh
COPY codex/run-codex.sh /runner/run-codex.sh

RUN chmod +x /runner/common/agent-runner-lib.sh /runner/run-codex.sh

WORKDIR /workspace

ENTRYPOINT ["/runner/run-codex.sh"]