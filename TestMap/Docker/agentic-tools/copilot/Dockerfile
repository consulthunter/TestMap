FROM testmap-dotnet-agent-base:0.1

# Install GitHub Copilot CLI.
# This uses GitHub's installer. If GitHub later provides a stable pinned
# package/binary download, prefer pinning for reproducible evaluations.
RUN curl -fsSL https://gh.io/copilot-install | bash

# Shared runner helper and Copilot-specific execution script.
COPY common/agent-runner-lib.sh /runner/common/agent-runner-lib.sh
COPY copilot/run-copilot.sh /runner/run-copilot.sh

RUN chmod +x /runner/common/agent-runner-lib.sh /runner/run-copilot.sh

WORKDIR /workspace

USER testmap-agent

ENTRYPOINT ["/runner/run-copilot.sh"]
