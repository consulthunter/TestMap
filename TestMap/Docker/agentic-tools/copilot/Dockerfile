FROM testmap-dotnet-agent-base:0.1

ARG COPILOT_CLI_VERSION=1.0.80

# Install GitHub Copilot CLI from npm rather than the gh.io installer, which
# always fetches the current release and cannot be pinned. The package publishes
# the same "copilot" binary the runner invokes. The version is pinned so an
# evaluation sweep cannot silently change tool behaviour partway through; bump
# it deliberately.
RUN npm install -g @github/copilot@${COPILOT_CLI_VERSION}

# Shared runner helper and Copilot-specific execution script.
COPY common/agent-runner-lib.sh /runner/common/agent-runner-lib.sh
COPY copilot/run-copilot.sh /runner/run-copilot.sh

RUN chmod +x /runner/common/agent-runner-lib.sh /runner/run-copilot.sh

WORKDIR /workspace

USER testmap-agent

ENTRYPOINT ["/runner/run-copilot.sh"]
