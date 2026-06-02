FROM testmap-dotnet-agent-base:0.1

ARG MINI_SWE_AGENT_VERSION=latest

# Install mini-SWE-agent using uv.
# For reproducible evaluation runs, replace "latest" with a pinned version
# after the pilot stabilizes.
RUN if [ "$MINI_SWE_AGENT_VERSION" = "latest" ]; then \
      uv tool install mini-swe-agent; \
    else \
      uv tool install "mini-swe-agent==${MINI_SWE_AGENT_VERSION}"; \
    fi

# Shared runner helper and mini-SWE-agent-specific execution script.
COPY common/agent-runner-lib.sh /runner/common/agent-runner-lib.sh
COPY mini-swe-agent/run-mini.sh /runner/run-mini-swe-agent.sh

RUN chmod +x /runner/common/agent-runner-lib.sh /runner/run-mini-swe-agent.sh

WORKDIR /workspace

ENTRYPOINT ["/runner/run-mini-swe-agent.sh"]
