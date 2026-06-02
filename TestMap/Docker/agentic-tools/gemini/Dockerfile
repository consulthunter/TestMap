FROM testmap-dotnet-agent-base:0.1

ARG GEMINI_CLI_VERSION=latest

# Install the official Google Gemini CLI.
# For reproducible evaluation runs, replace "latest" with a pinned version
# after the pilot stabilizes.
RUN npm install -g @google/gemini-cli@${GEMINI_CLI_VERSION}

# Shared runner helper and Gemini-specific execution script.
COPY common/agent-runner-lib.sh /runner/common/agent-runner-lib.sh
COPY gemini/run-gemini.sh /runner/run-gemini.sh

RUN chmod +x /runner/common/agent-runner-lib.sh /runner/run-gemini.sh

WORKDIR /workspace

ENTRYPOINT ["/runner/run-gemini.sh"]
