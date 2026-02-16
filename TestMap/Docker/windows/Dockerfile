# Use Windows .NET SDK base image
FROM mcr.microsoft.com/dotnet/sdk:10.0-windowsservercore-ltsc2022

SHELL ["powershell", "-Command"]

WORKDIR /app

# Allow roll-forward
ENV DOTNET_ROLL_FORWARD=latestMajor

# Install multiple .NET SDKs using dotnet-install.ps1
# Download installer
RUN Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1

# Install multiple SDKs
RUN Set-ExecutionPolicy Bypass -Scope Process -Force
RUN ./dotnet-install.ps1 -Channel 9.0 -InstallDir 'C:\Program Files\dotnet'
RUN ./dotnet-install.ps1 -Channel 8.0 -InstallDir 'C:\Program Files\dotnet'
RUN ./dotnet-install.ps1 -Channel 7.0 -InstallDir 'C:\Program Files\dotnet'
RUN ./dotnet-install.ps1 -Channel 6.0 -InstallDir 'C:\Program Files\dotnet'

# Install global dotnet tools
RUN dotnet tool install -g dotnet-stryker
RUN dotnet tool install -g dotnet-coverage

# Install Python (Windows)
RUN Invoke-WebRequest "https://www.python.org/ftp/python/3.12.2/python-3.12.2-amd64.exe" -OutFile python-installer.exe ; \
    Start-Process python-installer.exe -ArgumentList '/quiet InstallAllUsers=1 PrependPath=1' -Wait ; \
    Remove-Item python-installer.exe

# Create venv + install Lizard
RUN python -m venv C:\lizardenv ; \
    C:\lizardenv\Scripts\pip install --upgrade pip ; \
    C:\lizardenv\Scripts\pip install lizard

# Add scripts
RUN New-Item -ItemType Directory -Path C:\app\scripts -Force
COPY scripts/*.ps1 C:/app/scripts/