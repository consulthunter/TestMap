FROM testmap-dotnet-agent-base:0.1

ARG CLAUDE_CODE_VERSION=latest

# Install Claude Code CLI.
# For reproducible evaluation runs, replace "latest" with a pinned version
# after the pilot stabilizes.
RUN npm install -g @anthropic-ai/claude-code@${CLAUDE_CODE_VERSION}

# Shared runner helper and Claude-specific execution script.
COPY common/agent-runner-lib.sh /runner/common/agent-runner-lib.sh
COPY claude/run-claude.sh /runner/run-claude.sh

RUN chmod +x /runner/common/agent-runner-lib.sh /runner/run-claude.sh

WORKDIR /workspace

USER testmap-agent

ENTRYPOINT ["/runner/run-claude.sh"]
