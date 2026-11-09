FROM testmap-dotnet-agent-base:0.1

ARG GEMINI_CLI_VERSION=0.55.1

# Install the official Google Gemini CLI.
# The version is pinned so an evaluation sweep cannot silently change tool
# behaviour partway through; bump it deliberately.
RUN npm install -g @google/gemini-cli@${GEMINI_CLI_VERSION}

# Shared runner helper and Gemini-specific execution script.
COPY common/agent-runner-lib.sh /runner/common/agent-runner-lib.sh
COPY gemini/run-gemini.sh /runner/run-gemini.sh

RUN chmod +x /runner/common/agent-runner-lib.sh /runner/run-gemini.sh

WORKDIR /workspace

USER testmap-agent

ENTRYPOINT ["/runner/run-gemini.sh"]
