FROM testmap-dotnet-agent-base:0.1

ARG AIDER_VERSION=latest

# Install Aider using uv.
# For reproducible evaluation runs, replace "latest" with a pinned version
# after the pilot stabilizes.
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

ENTRYPOINT ["/runner/run-aider.sh"]