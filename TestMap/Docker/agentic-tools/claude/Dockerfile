FROM testmap-dotnet-agent-base:0.1

ARG CLAUDE_CODE_VERSION=2.1.233

# Install Claude Code CLI.
# The version is pinned so an evaluation sweep cannot silently change tool
# behaviour partway through; bump it deliberately.
RUN npm install -g @anthropic-ai/claude-code@${CLAUDE_CODE_VERSION}

# Shared runner helper and Claude-specific execution script.
COPY common/agent-runner-lib.sh /runner/common/agent-runner-lib.sh
COPY claude/run-claude.sh /runner/run-claude.sh

RUN chmod +x /runner/common/agent-runner-lib.sh /runner/run-claude.sh

WORKDIR /workspace

USER testmap-agent

ENTRYPOINT ["/runner/run-claude.sh"]
