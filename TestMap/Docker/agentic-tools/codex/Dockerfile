FROM testmap-dotnet-agent-base:0.1

ARG CODEX_CLI_VERSION=0.147.0

# Install Codex CLI.
# The version is pinned so an evaluation sweep cannot silently change tool
# behaviour partway through; bump it deliberately.
RUN npm install -g @openai/codex@${CODEX_CLI_VERSION}

# Shared runner helper and Codex-specific execution script.
COPY common/agent-runner-lib.sh /runner/common/agent-runner-lib.sh
COPY codex/run-codex.sh /runner/run-codex.sh

RUN chmod +x /runner/common/agent-runner-lib.sh /runner/run-codex.sh

WORKDIR /workspace

USER testmap-agent

ENTRYPOINT ["/runner/run-codex.sh"]
