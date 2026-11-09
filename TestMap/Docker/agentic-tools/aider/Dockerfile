FROM testmap-dotnet-agent-base:0.1

ARG AIDER_VERSION=0.86.2

# Install Aider using uv. The version is pinned so an evaluation sweep cannot
# silently change tool behaviour partway through; bump it deliberately.
RUN if [ "$AIDER_VERSION" = "latest" ]; then \
      uv tool install aider-chat; \
    else \
      uv tool install "aider-chat==${AIDER_VERSION}"; \
    fi

# Shared runner helper and Aider-specific execution script.
COPY common/agent-runner-lib.sh /runner/common/agent-runner-lib.sh
COPY aider/run-aider.sh /runner/run-aider.sh

RUN chmod +x /runner/common/agent-runner-lib.sh /runner/run-aider.sh

WORKDIR /workspace

USER testmap-agent

ENTRYPOINT ["/runner/run-aider.sh"]
