FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /app

# Use latest major roll-forward to allow global.json flexibility
ENV DOTNET_ROLL_FORWARD=latestMajor

# Install multiple SDKs and ensure DOTNET_ROOT is set
RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh \
    && chmod +x dotnet-install.sh \
    && ./dotnet-install.sh --channel 9.0 --install-dir /usr/share/dotnet \
    && ./dotnet-install.sh --channel 8.0 --install-dir /usr/share/dotnet \
    && ./dotnet-install.sh --channel 7.0 --install-dir /usr/share/dotnet \
    && ./dotnet-install.sh --channel 6.0 --install-dir /usr/share/dotnet

# Make sure dotnet CLI can see all SDKs
ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH="${DOTNET_ROOT}:${PATH}"

# Install Stryker globally
RUN dotnet tool install -g dotnet-stryker
RUN dotnet tool install -g dotnet-coverage
RUN dotnet tool install --global dotnet-reportgenerator-globaltool
ENV PATH="${PATH}:/root/.dotnet/tools"
ENV PYTHONPATH="/app/runner/src"

# Install Python for the TestMap runner
RUN apt-get update && apt-get install -y python3 \
    && rm -rf /var/lib/apt/lists/*
    
# Add runner
RUN mkdir -p /app/runner/src
COPY runner/src/testmap_runner /app/runner/src/testmap_runner
