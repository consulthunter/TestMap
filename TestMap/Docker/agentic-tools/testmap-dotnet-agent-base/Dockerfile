FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /workspace

# Allow repos with global.json to roll forward across installed SDKs.
ENV DOTNET_ROLL_FORWARD=latestMajor
ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH="${DOTNET_ROOT}:${PATH}"

# Basic tools for agent execution, .NET projects, Node-based CLIs, and Python/uv-based CLIs.
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    wget \
    git \
    jq \
    ripgrep \
    unzip \
    bash \
    procps \
    python3 \
    python3-venv \
    python3-pip \
    && rm -rf /var/lib/apt/lists/*

# Install a current Node runtime for Node-based agent CLIs. Debian's default
# package is Node 18 in this base image, which is too old for Gemini CLI.
RUN curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && node --version \
    && npm --version \
    && rm -rf /var/lib/apt/lists/*

# Install multiple .NET SDK channels so repositories with older global.json files can build.
RUN wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh \
    && chmod +x /tmp/dotnet-install.sh \
    && /tmp/dotnet-install.sh --channel 9.0 --install-dir /usr/share/dotnet \
    && /tmp/dotnet-install.sh --channel 8.0 --install-dir /usr/share/dotnet \
    && /tmp/dotnet-install.sh --channel 7.0 --install-dir /usr/share/dotnet \
    && /tmp/dotnet-install.sh --channel 6.0 --install-dir /usr/share/dotnet \
    && rm /tmp/dotnet-install.sh

# Install uv for Python-based agent tools such as OpenHands and mini-SWE-agent.
RUN curl -LsSf https://astral.sh/uv/install.sh | sh

# uv installs into /root/.local/bin by default.
ENV PATH="/root/.local/bin:${PATH}"

# Reduce noisy first-run behavior.
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
ENV NUGET_XMLDOC_MODE=skip

# Shared runner utilities are copied by tool-specific images using the agentic-tools build context.
# Expected runtime mounts:
#   /workspace -> repository checkout
#   /attempt   -> task card, prompt, logs, metadata, patch

WORKDIR /workspace
