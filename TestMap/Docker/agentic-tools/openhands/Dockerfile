FROM testmap-dotnet-agent-base:0.1

ARG OPENHANDS_VERSION=latest

# Install OpenHands using uv.
# For reproducible evaluation runs, replace "latest" with a pinned version
# after the pilot stabilizes.
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
