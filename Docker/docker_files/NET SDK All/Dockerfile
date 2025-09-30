# Use a base image with a Debian-based OS
FROM alpine:latest

# Set the working directory
WORKDIR /app

# Install dependencies using apk (Alpine's package manager)
RUN apk add --no-cache \
    bash \
    curl \
    icu-libs \
    libintl \
    && rm -rf /var/cache/apk/*

# Install .NET SDK 6.0
RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh

RUN chmod +x ./dotnet-install.sh

RUN ./dotnet-install.sh --channel 10.0
RUN ./dotnet-install.sh --channel 9.0
RUN ./dotnet-install.sh --channel 8.0
RUN ./dotnet-install.sh --channel 7.0
RUN ./dotnet-install.sh --channel 6.0
RUN ./dotnet-install.sh --channel 5.0

# Copy scripts and grant execution permissions
RUN mkdir -p /app/scripts/
COPY scripts/*.sh /app/scripts/
RUN chmod +x /app/scripts/*.sh

# Make sure that the .dotnet tools directory is on the PATH
ENV PATH="${PATH}:/root/.dotnet/:/root/.dotnet/tools"
