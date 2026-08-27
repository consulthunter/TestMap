FROM testmap-dotnet-agent-base:0.1

ARG OPENHANDS_VERSION=1.16.0

# Install OpenHands using uv. The version is pinned so an evaluation sweep cannot
# silently change tool behaviour partway through; bump it deliberately.
RUN if [ "$OPENHANDS_VERSION" = "latest" ]; then \
      uv tool install openhands --python 3.12; \
    else \
      uv tool install "openhands==${OPENHANDS_VERSION}" --python 3.12; \
    fi

# Shared runner helper and OpenHands-specific execution script.
COPY common/agent-runner-lib.sh /runner/common/agent-runner-lib.sh
COPY openhands/run-openhands.sh /runner/run-openhands.sh

RUN chmod +x /runner/common/agent-runner-lib.sh /runner/run-openhands.sh

WORKDIR /workspace

USER testmap-agent

ENTRYPOINT ["/runner/run-openhands.sh"]
