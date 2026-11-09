FROM testmap-dotnet-agent-base:0.1

ARG MINI_SWE_AGENT_VERSION=2.4.6

# Install mini-SWE-agent using uv.
#
# litellm reaches into litellm.proxy on its completion path, and those modules
# import fastapi and orjson, which the base litellm install does not provide.
# Without the proxy extra every model call fails with ModuleNotFoundError and
# litellm retries it as though it were transient, so an attempt burns several
# minutes before exiting non-zero having produced nothing.
RUN if [ "$MINI_SWE_AGENT_VERSION" = "latest" ]; then \
      uv tool install mini-swe-agent --with "litellm[proxy]"; \
    else \
      uv tool install "mini-swe-agent==${MINI_SWE_AGENT_VERSION}" --with "litellm[proxy]"; \
    fi \
    && "${UV_TOOL_DIR}/mini-swe-agent/bin/python" -c \
      "import litellm.responses.mcp.chat_completions_handler"

# Shared runner helper and mini-SWE-agent-specific execution script.
COPY common/agent-runner-lib.sh /runner/common/agent-runner-lib.sh
COPY mini-swe-agent/run-mini.sh /runner/run-mini-swe-agent.sh

RUN chmod +x /runner/common/agent-runner-lib.sh /runner/run-mini-swe-agent.sh

WORKDIR /workspace

USER testmap-agent

ENTRYPOINT ["/runner/run-mini-swe-agent.sh"]
