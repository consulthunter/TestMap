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
ENV PATH="${PATH}:/root/.dotnet/tools"


# Install Python + venv + Lizard
RUN apt-get update && apt-get install -y python3 python3-venv python3-pip \
    && python3 -m venv /opt/lizardenv \
    && /opt/lizardenv/bin/pip install --upgrade pip \
    && /opt/lizardenv/bin/pip install lizard \
    && rm -rf /var/lib/apt/lists/* 
    
# Add scripts
RUN mkdir -p /app/scripts
COPY scripts/*.sh /app/scripts/
RUN chmod +x /app/scripts/*.sh
